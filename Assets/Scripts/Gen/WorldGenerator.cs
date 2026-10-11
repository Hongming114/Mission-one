using System.Collections.Generic;
using UnityEngine;
using DungeonCity.Core;

namespace DungeonCity.Gen
{
    /// <summary>
    /// 世界生成入口：I 类矩阵 -> 房间 -> (II, III) -> 门连接 -> 出生点 -> 唯一的跨世界梯子。
    /// </summary>
    public static class WorldGenerator
    {
        public static WorldData Generate(WorldKind kind, int seed)
            => Generate(WorldCatalog.For(kind), seed);

        public static WorldData Generate(WorldGenConfig cfg, int seed)
        {
            var world = new WorldData
            {
                kind = cfg.kind,
                seed = seed,
                iWidth = cfg.iMatrix.GetLength(0),
                iHeight = cfg.iMatrix.GetLength(1),
                iMatrix = cfg.iMatrix,
            };

            // 1) 收集房间，按"邻居方位"决定每间房开哪几个门
            var keys = new List<Vector2Int>();
            for (int y = 0; y < world.iHeight; y++)
                for (int x = 0; x < world.iWidth; x++)
                    if (cfg.iMatrix[x, y] != RoomTypeId.None) keys.Add(new Vector2Int(x, y));

            if (keys.Count == 0) throw new System.Exception("[WorldGenerator] I 类矩阵里没有任何房间");

            foreach (var key in keys)
            {
                int typeId = cfg.iMatrix[key.x, key.y];
                RoomProfile profile = cfg.ProfileOf(typeId);
                DoorDir doors = ComputeDoors(cfg.iMatrix, world.iWidth, world.iHeight, key);
                int roomSeed = RoomSeed(seed, key);
                Vector3 origin = new Vector3(key.x * cfg.spacing, 0f, key.y * cfg.spacing);
                world.rooms[key] = RoomBuilder.Build(profile, key, doors, roomSeed, origin);
            }

            // 2) 门对：左房间的东门 <-> 右房间的西门（只按方位对应，坐标可以不同）
            WorldGenerator.LinkPortals(world, keys);

            // 3) 出生房间：I 类矩阵坐标
            world.spawnRoom = cfg.spawnRoomGrid.x >= 0 ? cfg.spawnRoomGrid : keys[0];
            if (!world.HasRoom(world.spawnRoom))
                throw new System.Exception($"[WorldGenerator] 出生房间 {world.spawnRoom} 在 I 类矩阵里不是房间");

            // 4) 跨世界梯子：整个世界里唯一一个 II=3 的格子（先放梯子，出生格要避开它）
            Vector2Int ladderKey = cfg.worldPortalRoomGrid.x >= 0
                ? cfg.worldPortalRoomGrid
                : FarthestRoomFrom(world, world.spawnRoom);
            RoomData ladderRoom = world.Room(ladderKey);
            if (ladderRoom == null)
                throw new System.Exception($"[WorldGenerator] 梯子房间 {ladderKey} 在 I 类矩阵里不是房间");
            world.worldPortalRoom = ladderKey;
            world.worldPortalCell = RoomBuilder.PlaceLadder(ladderRoom);

            // 5) 出生格：房间内离中心最近的可站立格，避开梯子格（梯子房间同时也是出生房间时必须）
            world.spawnCell = RoomBuilder.FindStandableCell(
                world.SpawnRoom,
                world.worldPortalRoom == world.spawnRoom ? world.worldPortalCell : (Vector2Int?)null);

            // 6) 自检
            WorldValidator.Validate(world);
            return world;
        }

        private static void LinkPortals(WorldData world, List<Vector2Int> keys)
        {
            foreach (var key in keys)
            {
                RoomData room = world.rooms[key];
                foreach (Dir dir in DirUtil.All)
                {
                    if (!DoorDirUtil.Has(room.doors, DoorDirUtil.Of(dir))) continue;
                    Vector2Int nk = key + DirUtil.Step(dir);
                    RoomData neighbour = world.Room(nk);
                    if (neighbour == null) continue;   // 门只在有邻居的方位开，正常不会发生
                    // 每对相邻房间只记录一条连接：按 (y, x) 字典序只从较小的一侧记录，否则会被记两次
                    if (nk.y < key.y || (nk.y == key.y && nk.x < key.x)) continue;
                    Dir back = DirUtil.Opposite(dir);
                    world.AddPortal(new RoomPortal
                    {
                        roomA = key,
                        cellA = DoorCell(room, dir),
                        roomB = nk,
                        cellB = DoorCell(neighbour, back),
                        dirFromA = dir,
                    });
                }
            }
        }

        /// <summary>门在墙上的固定位置：该面墙（四角除外）的中点。</summary>
        public static Vector2Int DoorCell(RoomData room, Dir dir)
        {
            switch (dir)
            {
                case Dir.North: return new Vector2Int(room.width / 2, room.height - 1);
                case Dir.South: return new Vector2Int(room.width / 2, 0);
                case Dir.East: return new Vector2Int(room.width - 1, room.height / 2);
                default: return new Vector2Int(0, room.height / 2);
            }
        }

        private static DoorDir ComputeDoors(int[,] m, int w, int h, Vector2Int key)
        {
            DoorDir doors = DoorDir.None;
            foreach (Dir dir in DirUtil.All)
            {
                Vector2Int s = DirUtil.Step(dir);
                int nx = key.x + s.x, ny = key.y + s.y;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                if (m[nx, ny] != RoomTypeId.None) doors |= DoorDirUtil.Of(dir);
            }
            return doors;
        }

        private static int RoomSeed(int seed, Vector2Int key)
        {
            unchecked
            {
                int s = seed * 73856093 ^ key.x * 19349663 ^ key.y * 83492791;
                return s & 0x7FFFFFFF;
            }
        }

        /// <summary>离给定房间最远的房间（房间距离用 BFS，房间数最少的那条路）。</summary>
        public static Vector2Int FarthestRoomFrom(WorldData world, Vector2Int from)
        {
            var dist = RoomDistanceField.Distances(world, from);
            Vector2Int best = from;
            int bestD = -1;
            foreach (var key in SortedKeys(world))
            {
                if (key == from) continue;
                int d;
                if (!dist.TryGetValue(key, out d)) continue;
                if (d > bestD) { bestD = d; best = key; }
            }
            return best;
        }

        public static List<Vector2Int> SortedKeys(WorldData world)
        {
            var list = new List<Vector2Int>(world.rooms.Keys);
            list.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            return list;
        }
    }
}