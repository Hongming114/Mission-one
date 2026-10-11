using System.Collections.Generic;
using UnityEngine;
using DungeonCity.Core;

namespace DungeonCity.Gen
{
    /// <summary>
    /// 房间之间的一对门。左房间的东门 <-> 右房间的西门：只按方位对应，不要求坐标相同。
    /// </summary>
    public class RoomPortal
    {
        public Vector2Int roomA, cellA;
        public Vector2Int roomB, cellB;
        public Dir dirFromA;   // A 房间在这个方位开的门

        public string Describe() =>
            $"房间({roomA.x},{roomA.y})格({cellA.x},{cellA.y}) 往{DirUtil.Short(dirFromA)} <-> 房间({roomB.x},{roomB.y})格({cellB.x},{cellB.y})";
    }

    /// <summary>
    /// 一个世界的全部生成结果（M=1 的城市或 M=2 的地牢都用它承载）。
    /// </summary>
    public class WorldData
    {
        public WorldKind kind;
        public int seed;
        public int iWidth, iHeight;
        public int[,] iMatrix;                                    // I 类矩阵
        public Dictionary<Vector2Int, RoomData> rooms = new Dictionary<Vector2Int, RoomData>();
        public List<RoomPortal> portals = new List<RoomPortal>();

        private readonly Dictionary<(Vector2Int room, Vector2Int cell), RoomPortal> _portalAt =
            new Dictionary<(Vector2Int, Vector2Int), RoomPortal>();

        public Vector2Int spawnRoom = new Vector2Int(-1, -1);     // 出生房间
        public Vector2Int spawnCell = new Vector2Int(-1, -1);     // 出生格（III != 0）
        public Vector2Int worldPortalRoom = new Vector2Int(-1, -1);// 梯子所在房间
        public Vector2Int worldPortalCell = new Vector2Int(-1, -1);// 梯子格（III = 2.2）

        public string KindName => kind == WorldKind.City ? "城市" : "地牢";
        public int RoomCount => rooms.Count;
        public IEnumerable<RoomData> AllRooms => rooms.Values;

        public RoomData Room(Vector2Int key)
        {
            RoomData r;
            return rooms.TryGetValue(key, out r) ? r : null;
        }
        public bool HasRoom(Vector2Int key) => rooms.ContainsKey(key);
        public RoomData SpawnRoom => Room(spawnRoom);
        public RoomData WorldPortalRoom => Room(worldPortalRoom);

        public void AddPortal(RoomPortal p)
        {
            portals.Add(p);
            _portalAt[(p.roomA, p.cellA)] = p;
            _portalAt[(p.roomB, p.cellB)] = p;
        }

        public RoomPortal PortalAt(Vector2Int room, Vector2Int cell)
        {
            RoomPortal p;
            return _portalAt.TryGetValue((room, cell), out p) ? p : null;
        }

        /// <summary>踩到门格时，查另一侧的房间与格子（方位对应，坐标可以不同）。</summary>
        public bool TryGetPortalTarget(Vector2Int room, Vector2Int cell, out Vector2Int targetRoom, out Vector2Int targetCell)
        {
            targetRoom = new Vector2Int(-1, -1);
            targetCell = new Vector2Int(-1, -1);
            var p = PortalAt(room, cell);
            if (p == null) return false;
            if (p.roomA == room && p.cellA == cell) { targetRoom = p.roomB; targetCell = p.cellB; }
            else { targetRoom = p.roomA; targetCell = p.cellA; }
            return true;
        }

        public string Describe()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"世界 M={(int)kind} ({KindName}) 种子={seed} I 矩阵 {iWidth}x{iHeight} 房间数={RoomCount} 门对={portals.Count}");
            sb.AppendLine($"出生房间 {spawnRoom} 出生格 {spawnCell}");
            sb.AppendLine($"跨世界梯子 房间 {worldPortalRoom} 格 {worldPortalCell}");
            foreach (var kv in rooms) sb.AppendLine("  " + kv.Value);
            return sb.ToString();
        }
    }
}