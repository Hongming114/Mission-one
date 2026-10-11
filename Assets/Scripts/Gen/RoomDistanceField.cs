using System.Collections.Generic;
using UnityEngine;
using DungeonCity.Core;

namespace DungeonCity.Gen
{
    /// <summary>
    /// 房间距离场：距离 = 离玩家所在房间至少要经过多少个房间。玩家所在房间 = 0。
    /// 只沿"有门的相邻房间"扩散。
    /// </summary>
    public static class RoomDistanceField
    {
        public static Dictionary<Vector2Int, int> Distances(WorldData world, Vector2Int from)
        {
            var dist = new Dictionary<Vector2Int, int>();
            if (!world.HasRoom(from)) return dist;

            dist[from] = 0;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                RoomData room = world.Room(cur);
                foreach (Dir dir in DirUtil.All)
                {
                    if (!DoorDirUtil.Has(room.doors, DoorDirUtil.Of(dir))) continue;   // 该方位没门 -> 走不通
                    Vector2Int nk = cur + DirUtil.Step(dir);
                    if (world.Room(nk) == null) continue;
                    if (dist.ContainsKey(nk)) continue;
                    dist[nk] = dist[cur] + 1;
                    queue.Enqueue(nk);
                }
            }
            return dist;
        }

        public static int Distance(WorldData world, Vector2Int from, Vector2Int to)
        {
            var d = Distances(world, from);
            int v;
            return d.TryGetValue(to, out v) ? v : -1;
        }
    }
}