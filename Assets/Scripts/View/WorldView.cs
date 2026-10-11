using System.Collections.Generic;
using UnityEngine;
using DungeonCity.Core;
using DungeonCity.Gen;

namespace DungeonCity.View
{
    /// <summary>
    /// 一个世界（城市或地牢）的全部房间视图。房间按 I 类矩阵坐标摆在世界空间里，
    /// 房间之间是"伪连接"（门不对齐、中间留空隙），跨房间靠 III 类矩阵的传送功能。
    /// </summary>
    public class WorldView
    {
        public WorldData data;
        public GameObject root;
        public readonly Dictionary<Vector2Int, RoomView> rooms = new Dictionary<Vector2Int, RoomView>();

        public static WorldView Build(WorldData data, MaterialLibrary mats, Transform parent)
        {
            var view = new WorldView { data = data };
            view.root = new GameObject($"世界_M{(int)data.kind}_{data.KindName}");
            if (parent != null) view.root.transform.SetParent(parent, false);

            WorldGenConfig cfg = WorldCatalog.For(data.kind);
            foreach (Vector2Int key in WorldGenerator.SortedKeys(data))
            {
                RoomData room = data.Room(key);
                RoomProfile p = cfg.ProfileOf(room.typeId);
                view.rooms[key] = RoomView.Build(room, view.root.transform, mats, p);
            }
            return view;
        }

        /// <summary>反馈 3：只显示玩家所在房间，其他房间整体隐藏（跨房间看不到）。</summary>
        public void ShowOnlyRoom(Vector2Int key)
        {
            foreach (var kv in rooms)
            {
                if (kv.Value != null && kv.Value.root != null) kv.Value.root.SetActive(kv.Key == key);
            }
        }

        /// <summary>把所有房间显示出来（截图 / 总览图用）。</summary>
        public void ShowAllRooms()
        {
            foreach (var kv in rooms)
            {
                if (kv.Value != null && kv.Value.root != null) kv.Value.root.SetActive(true);
            }
        }

        /// <summary>调试2-4：显示给定的房间集合（玩家房间 + 门相连的相邻房间 + 相机视野内的房间）。</summary>
        public void ShowRooms(ICollection<Vector2Int> keys)
        {
            foreach (var kv in rooms)
            {
                if (kv.Value == null || kv.Value.root == null) continue;
                kv.Value.root.SetActive(keys != null && keys.Contains(kv.Key));
            }
        }

        public void SetVisible(bool visible)
        {
            if (root != null) root.SetActive(visible);
        }

        public Vector3 CellWorldPosition(Vector2Int roomKey, Vector2Int cell)
        {
            RoomData room = data.Room(roomKey);
            if (room == null) return Vector3.zero;
            return room.CellCenter(cell.x, cell.y);
        }

        /// <summary>把整个世界的房间都框进来（相机取景 / 截图用）。</summary>
        public Bounds WorldBounds()
        {
            bool has = false;
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var kv in rooms)
            {
                RoomData r = kv.Value.data;
                Vector3 c = r.worldOrigin + new Vector3(r.width * 0.5f, 0f, r.height * 0.5f);
                var rb = new Bounds(c, new Vector3(r.width, 2.2f, r.height));
                if (!has) { b = rb; has = true; } else b.Encapsulate(rb);
            }
            return b;
        }

        public RoomView RoomViewAt(Vector2Int key)
        {
            RoomView v;
            return rooms.TryGetValue(key, out v) ? v : null;
        }
    }
}