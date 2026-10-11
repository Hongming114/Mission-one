using System.Collections.Generic;
using UnityEngine;
using DungeonCity.Core;

namespace DungeonCity.Gen
{
    /// <summary>房间内部的可达性工具：III 类矩阵里所有非 0 的格子必须互相连通（四连通）。</summary>
    public static class RoomConnectivity
    {
        private static readonly Vector2Int[] Steps =
        {
            new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0),
        };

        /// <summary>房间内可以站人的格子总数（III != 0）。</summary>
        public static int StandableCount(RoomData room)
        {
            int n = 0;
            for (int y = 0; y < room.height; y++)
                for (int x = 0; x < room.width; x++)
                    if (room.CanStand(x, y)) n++;
            return n;
        }

        public static int ReachableCount(RoomData room)
        {
            Vector2Int? start = null;
            for (int y = 0; y < room.height && start == null; y++)
                for (int x = 0; x < room.width && start == null; x++)
                    if (room.CanStand(x, y)) start = new Vector2Int(x, y);
            if (start == null) return 0;

            var visited = new bool[room.width, room.height];
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start.Value);
            visited[start.Value.x, start.Value.y] = true;
            int count = 1;

            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                foreach (var s in Steps)
                {
                    int nx = c.x + s.x, ny = c.y + s.y;
                    if (!room.InBounds(nx, ny)) continue;
                    if (visited[nx, ny]) continue;
                    if (!room.CanStand(nx, ny)) continue;
                    visited[nx, ny] = true;
                    count++;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
            return count;
        }

        public static bool IsConnected(RoomData room) => ReachableCount(room) == StandableCount(room);
    }
}