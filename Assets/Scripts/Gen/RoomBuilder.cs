using System.Collections.Generic;
using UnityEngine;
using DungeonCity.Core;

namespace DungeonCity.Gen
{
    /// <summary>
    /// 单个房间的生成器：房间类型参数 + 邻居方位 -> II 类矩阵 -> III 类矩阵。
    /// 这里是"II 类与 III 类如何对应"的唯一权威实现。
    /// </summary>
    public static class RoomBuilder
    {
        public static RoomData Build(RoomProfile p, Vector2Int grid, DoorDir doors, int seed, Vector3 worldOrigin)
        {
            var rng = new System.Random(seed);
            int w = rng.Next(p.minWidth, p.maxWidth + 1);      // a: 10-15，每个房间各自随机
            int h = rng.Next(p.minHeight, p.maxHeight + 1);    // b: 10-15

            var room = new RoomData
            {
                grid = grid,
                typeId = p.typeId,
                typeName = p.displayName,
                width = w,
                height = h,
                tiles = new int[w, h],
                func = new float[w, h],
                doors = doors,
                worldOrigin = worldOrigin,
            };

            // 1) 先铺地板，再把最外一圈变成外墙
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool border = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    room.tiles[x, y] = border ? (int)TileCode.Wall : (int)TileCode.Floor;
                }

            // 2) 每面墙（四角除外）取中点开一个门：只有该方位存在相邻房间时才开
            if (DoorDirUtil.Has(doors, DoorDir.South)) room.tiles[w / 2, 0] = (int)TileCode.Door;
            if (DoorDirUtil.Has(doors, DoorDir.North)) room.tiles[w / 2, h - 1] = (int)TileCode.Door;
            if (DoorDirUtil.Has(doors, DoorDir.West)) room.tiles[0, h / 2] = (int)TileCode.Door;
            if (DoorDirUtil.Has(doors, DoorDir.East)) room.tiles[w - 1, h / 2] = (int)TileCode.Door;

            // 3) 房间内部装饰：只动 II 类矩阵，不碰门，也不碰中央十字通道（保证连通）
            if (p.cornerPillars) PlaceCornerPillars(room, p, rng);
            int rockTarget = p.rockCountMax > 0 ? rng.Next(p.rockCountMin, p.rockCountMax + 1) : 0;
            if (rockTarget > 0)
            {
                List<Vector2Int> rocks = PlaceRocks(room, p, rng, rockTarget);
                // 保险：如果碎石把某块地板围死了，就按放置顺序把最后放的石块撤掉
                while (rocks.Count > 0 && !RoomConnectivity.IsConnected(room))
                {
                    Vector2Int c = rocks[rocks.Count - 1];
                    rocks.RemoveAt(rocks.Count - 1);
                    room.tiles[c.x, c.y] = (int)TileCode.Floor;
                }
            }

            // 4) 由 II 类矩阵推导 III 类矩阵
            DeriveFunc(room);
            return room;
        }

        /// <summary>II -> III 的对应规则（唯一权威）：墙=0 不可达，地板=1 可达，门=2.1 房间传送，梯子=2.2 跨世界。</summary>
        public static float FuncOfTile(int tile)
        {
            switch ((TileCode)tile)
            {
                case TileCode.Wall: return FuncCode.Blocked;
                case TileCode.Door: return FuncCode.RoomPortal;
                case TileCode.Ladder: return FuncCode.WorldPortal;
                default: return FuncCode.Walkable;
            }
        }

        public static void DeriveFunc(RoomData room)
        {
            for (int y = 0; y < room.height; y++)
                for (int x = 0; x < room.width; x++)
                    room.func[x, y] = FuncOfTile(room.tiles[x, y]);
        }

        /// <summary>四角立柱：每个角各自按 pillarKeepChance 决定是否出现（同类型房间之间也有差异）。</summary>
        private static void PlaceCornerPillars(RoomData room, RoomProfile p, System.Random rng)
        {
            int w = room.width, h = room.height;
            int inset = Mathf.Clamp(p.pillarInset, 1, Mathf.Min(w, h) / 2 - 1);
            TryPillar(room, p, rng, inset, inset);
            TryPillar(room, p, rng, w - 1 - inset, inset);
            TryPillar(room, p, rng, inset, h - 1 - inset);
            TryPillar(room, p, rng, w - 1 - inset, h - 1 - inset);
        }

        private static void TryPillar(RoomData room, RoomProfile p, System.Random rng, int x, int y)
        {
            if (rng.NextDouble() >= p.pillarKeepChance) return;
            SetWallIfFloor(room, p, x, y);
        }

        private static void SetWallIfFloor(RoomData room, RoomProfile p, int x, int y)
        {
            if (!room.InBounds(x, y)) return;
            if (p.keepCrossFree && (x == room.width / 2 || y == room.height / 2)) return;
            if (room.tiles[x, y] == (int)TileCode.Floor) room.tiles[x, y] = (int)TileCode.Wall;
        }

        /// <summary>地牢的碎石/岩块：随机散落，保持中央十字通道与门口畅通。返回放置的格子。</summary>
        private static List<Vector2Int> PlaceRocks(RoomData room, RoomProfile p, System.Random rng, int count)
        {
            var placed = new List<Vector2Int>();
            int w = room.width, h = room.height;
            int attempts = 0, maxAttempts = count * 60 + 40;
            while (placed.Count < count && attempts < maxAttempts)
            {
                attempts++;
                int x = rng.Next(1, w - 1);
                int y = rng.Next(1, h - 1);
                if (p.keepCrossFree && (x == w / 2 || y == h / 2)) continue;
                if (room.tiles[x, y] != (int)TileCode.Floor) continue;
                if (NearDoor(room, x, y)) continue;
                room.tiles[x, y] = (int)TileCode.Wall;
                placed.Add(new Vector2Int(x, y));
            }
            return placed;
        }

        private static bool NearDoor(RoomData room, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (room.IsDoorCell(x + dx, y + dy)) return true;
            return false;
        }

        /// <summary>把梯子放进房间（II=3, III=2.2）。一个世界里只会调用一次。</summary>
        public static Vector2Int PlaceLadder(RoomData room)
        {
            Vector2Int cell = FindStandableCell(room);
            room.tiles[cell.x, cell.y] = (int)TileCode.Ladder;
            room.func[cell.x, cell.y] = FuncCode.WorldPortal;
            return cell;
        }

        /// <summary>离房间中心最近的可站立格子（III 类矩阵里非 0 的格）。</summary>
        public static Vector2Int FindStandableCell(RoomData room)
        {
            return FindStandableCell(room, null);
        }

        /// <summary>离房间中心最近的可站立格子（III 类矩阵里 =1 的普通可达格）；可排除某一格（例如梯子格）。</summary>
        public static Vector2Int FindStandableCell(RoomData room, Vector2Int? exclude)
        {
            Vector2Int best = new Vector2Int(room.width / 2, room.height / 2);
            float bestD = float.MaxValue;
            bool found = false;
            for (int y = 1; y < room.height - 1; y++)
                for (int x = 1; x < room.width - 1; x++)
                {
                    if (exclude.HasValue && exclude.Value.x == x && exclude.Value.y == y) continue;
                    if (!FuncCode.IsWalkable(room.func[x, y])) continue;   // 只挑"普通可达"的格子
                    float dx = x - room.width / 2f, dy = y - room.height / 2f;
                    float d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = new Vector2Int(x, y); found = true; }
                }
            if (!found) throw new System.Exception("[RoomBuilder] 房间内没有可站立格子: " + room.Key);
            return best;
        }
    }
}