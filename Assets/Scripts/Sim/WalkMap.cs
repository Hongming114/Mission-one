using System.Collections.Generic;
using UnityEngine;
using DungeonCity.Core;
using DungeonCity.Gen;

namespace DungeonCity.Sim
{
    /// <summary>
    /// 把一个世界的 III 类矩阵摊平成 O(1) 查询表，供连续移动（主角 / 怪物）做碰撞与"穿门"：
    ///   * 可走 = 该坐标落在某个房间内部 且 III 类矩阵不是 0（不可达格）；
    ///   * 门格（III=2.1）可走，踩上门格会被 TryExitPortal 送到配对门格的内侧；
    ///   * 跨世界梯子（III=2.2）对玩家可走（踩上去切世界），对怪物按墙处理（怪不跟去另一个世界）；
    ///   * 房间之间的空隙不在任何房间里，按墙处理。
    /// 房间原点 = I 类矩阵坐标 * spacing，所以世界坐标 -> 房间只要一步整除。
    /// </summary>
    public sealed class WalkMap
    {
        public readonly WorldData world;
        public readonly float spacing;
        readonly Dictionary<Vector2Int, RoomData> byGrid = new Dictionary<Vector2Int, RoomData>();
        readonly Dictionary<Vector2Int, Vector2Int> origin = new Dictionary<Vector2Int, Vector2Int>();

        public WalkMap(WorldData world, float spacing)
        {
            this.world = world;
            this.spacing = spacing <= 0f ? 18f : spacing;
            foreach (Vector2Int key in WorldGenerator.SortedKeys(world))
            {
                RoomData r = world.Room(key);
                if (r == null) continue;
                byGrid[key] = r;
                origin[key] = new Vector2Int(Mathf.RoundToInt(r.worldOrigin.x), Mathf.RoundToInt(r.worldOrigin.z));
            }
        }

        public static Vector2Int CellOf(Vector2 position)
        {
            return new Vector2Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
        }

        public static Vector2 CenterOf(Vector2Int worldCell)
        {
            return new Vector2(worldCell.x + 0.5f, worldCell.y + 0.5f);
        }

        public bool TryRoom(Vector2Int worldCell, out RoomData room, out Vector2Int local)
        {
            room = null;
            local = Vector2Int.zero;
            int gx = Mathf.FloorToInt(worldCell.x / spacing);
            int gy = Mathf.FloorToInt(worldCell.y / spacing);
            var grid = new Vector2Int(gx, gy);
            RoomData found;
            if (!byGrid.TryGetValue(grid, out found)) return false;
            Vector2Int o = origin[grid];
            local = worldCell - o;
            if (!found.InBounds(local.x, local.y)) return false;
            room = found;
            return true;
        }

        public bool TryRoomOf(Vector2 position, out RoomData room, out Vector2Int local)
        {
            return TryRoom(CellOf(position), out room, out local);
        }

        /// <summary>房间原点（格）+ 房间内格子 -> 世界格子坐标。</summary>
        public bool TryWorldCell(Vector2Int roomGrid, Vector2Int localCell, out Vector2Int worldCell)
        {
            worldCell = Vector2Int.zero;
            Vector2Int o;
            if (!origin.TryGetValue(roomGrid, out o)) return false;
            worldCell = o + localCell;
            return true;
        }

        public bool InsideAnyRoom(Vector2Int worldCell)
        {
            RoomData r; Vector2Int l;
            return TryRoom(worldCell, out r, out l);
        }

        public float FuncAt(Vector2Int worldCell)
        {
            RoomData room; Vector2Int local;
            if (!TryRoom(worldCell, out room, out local)) return 0f;   // 房间之外 = 不可达
            return room.Func(local.x, local.y);
        }

        public int TileAt(Vector2Int worldCell)
        {
            RoomData room; Vector2Int local;
            if (!TryRoom(worldCell, out room, out local)) return -1;
            return room.Tile(local.x, local.y);
        }

        public bool IsWalkable(Vector2Int worldCell, bool forPlayer)
        {
            RoomData room; Vector2Int local;
            if (!TryRoom(worldCell, out room, out local)) return false;
            float f = room.Func(local.x, local.y);
            if (FuncCode.IsBlocked(f)) return false;
            if (!forPlayer && FuncCode.IsWorldPortal(f)) return false;
            return true;
        }

        /// <summary>实体的方形体积（半边长 = radius）会不会压到墙 / 不可达格。连续移动的碰撞检测。</summary>
        public bool Blocked(Vector2 position, float radius, bool forPlayer)
        {
            int x0 = Mathf.FloorToInt(position.x - radius);
            int x1 = Mathf.FloorToInt(position.x + radius);
            int y0 = Mathf.FloorToInt(position.y - radius);
            int y1 = Mathf.FloorToInt(position.y + radius);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (!IsWalkable(new Vector2Int(x, y), forPlayer)) return true;
            return false;
        }

        /// <summary>
        /// 坐标在门格里就返回"穿门之后"的落脚点：配对门格的内侧（朝房间中心方向退 0.6 格），
        /// 免得刚落地又踩在同一个门格里来回传送。内侧被占（碎石等）时退回门格中心。
        /// </summary>
        public bool TryExitPortal(Vector2 position, float radius, bool forPlayer, out Vector2 exitPosition)
        {
            exitPosition = position;
            RoomData room; Vector2Int local;
            Vector2Int cell = CellOf(position);
            if (!TryRoom(cell, out room, out local)) return false;
            if (!FuncCode.IsRoomPortal(room.Func(local.x, local.y))) return false;
            Vector2Int targetRoomKey, targetLocal;
            if (!world.TryGetPortalTarget(room.grid, local, out targetRoomKey, out targetLocal)) return false;
            Vector2Int targetWorldCell;
            if (!TryWorldCell(targetRoomKey, targetLocal, out targetWorldCell)) return false;
            Vector2 center = CenterOf(targetWorldCell);
            RoomData targetRoom;
            Vector2Int ignore;
            if (!TryRoom(targetWorldCell, out targetRoom, out ignore)) return false;

            Vector2 inward = new Vector2(targetRoom.width * 0.5f - (targetLocal.x + 0.5f),
                                         targetRoom.height * 0.5f - (targetLocal.y + 0.5f));
            Vector2 dir = inward.sqrMagnitude > 1e-4f ? inward.normalized : new Vector2(0f, 1f);
            Vector2 candidate = center + dir * 0.6f;
            if (Blocked(candidate, radius, forPlayer)) candidate = center;
            exitPosition = candidate;
            return true;
        }

        public string Describe()
        {
            return $"WalkMap M={(int)world.kind} 房间 {byGrid.Count} 个 spacing {spacing}";
        }
    }
}