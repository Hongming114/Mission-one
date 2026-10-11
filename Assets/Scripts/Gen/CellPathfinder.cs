using System.Collections.Generic;
using UnityEngine;
using DungeonCity.Core;

namespace DungeonCity.Gen
{
    /// <summary>
    /// 格子级寻路（房间内走格子 + 门 = 传送到配对门格）。用途：
    ///   1) 自检"出生格能不能走到梯子格"这类可玩性问题；
    ///   2) 后续怪物 AI 的跨房间移动可以直接复用。
    /// 跨世界梯子（III=2.2）在这里是终点：它通向另一个世界，需要两个世界一起算，本类只管单世界。
    /// </summary>
    public static class CellPathfinder
    {
        public struct Node
        {
            public Vector2Int room;
            public Vector2Int cell;
            public Node(Vector2Int room, Vector2Int cell) { this.room = room; this.cell = cell; }
            public override string ToString() => $"房间({room.x},{room.y})格({cell.x},{cell.y})";
        }

        /// <summary>从起点出发，按移动规则能到达的所有格子（跨世界梯子是终点）。</summary>
        public static Dictionary<Node, int> Distances(WorldData world, Vector2Int roomKey, Vector2Int cell)
        {
            var dist = new Dictionary<Node, int>();
            var start = new Node(roomKey, cell);
            var queue = new Queue<Node>();
            dist[start] = 0;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                Node cur = queue.Dequeue();
                int d = dist[cur];
                RoomData room = world.Room(cur.room);
                if (room == null) continue;
                if (FuncCode.IsWorldPortal(room.Func(cur.cell.x, cur.cell.y))) continue;   // 梯子：不在单世界内继续展开

                foreach (Dir dir in DirUtil.All)
                {
                    Vector2Int s = DirUtil.Step(dir);
                    Vector2Int nc = cur.cell + s;
                    if (!room.InBounds(nc.x, nc.y)) continue;
                    if (FuncCode.IsBlocked(room.Func(nc.x, nc.y))) continue;

                    var next = new Node(cur.room, nc);
                    if (FuncCode.IsRoomPortal(room.Func(nc.x, nc.y)))
                    {
                        Vector2Int tr, tc;
                        if (!world.TryGetPortalTarget(cur.room, nc, out tr, out tc)) continue;
                        next = new Node(tr, tc);
                    }
                    if (dist.ContainsKey(next)) continue;
                    dist[next] = d + 1;
                    queue.Enqueue(next);
                }
            }
            return dist;
        }

        /// <summary>最短步数；不可达返回 -1。</summary>
        public static int StepsTo(WorldData world, Vector2Int fromRoom, Vector2Int fromCell, Vector2Int toRoom, Vector2Int toCell)
        {
            Dictionary<Node, int> dist = Distances(world, fromRoom, fromCell);
            int d;
            return dist.TryGetValue(new Node(toRoom, toCell), out d) ? d : -1;
        }

        /// <summary>最短路径（含起点与终点）；不可达返回空表。</summary>
        public static List<Node> Path(WorldData world, Vector2Int fromRoom, Vector2Int fromCell, Vector2Int toRoom, Vector2Int toCell)
        {
            Dictionary<Node, int> dist = Distances(world, fromRoom, fromCell);
            var goal = new Node(toRoom, toCell);
            var path = new List<Node>();
            if (!dist.ContainsKey(goal)) return path;

            Node cur = goal;
            path.Add(cur);
            while (dist[cur] > 0)
            {
                RoomData room = world.Room(cur.room);
                Node prevFound = default(Node);
                bool found = false;
                foreach (Dir dir in DirUtil.All)
                {
                    Vector2Int s = DirUtil.Step(dir);
                    Vector2Int pc = cur.cell - s;
                    if (!room.InBounds(pc.x, pc.y)) continue;
                    var cand = new Node(cur.room, pc);
                    if (!dist.ContainsKey(cand)) continue;
                    if (dist[cand] != dist[cur] - 1) continue;
                    prevFound = cand; found = true; break;
                }
                if (!found)
                {
                    // 当前格是门格：上一步是"从配对门格传送过来的"
                    foreach (RoomPortal p in world.portals)
                    {
                        if (p.roomB == cur.room && p.cellB == cur.cell)
                        {
                            var cand = new Node(p.roomA, p.cellA + DirUtil.Step(p.dirFromA));
                            if (dist.ContainsKey(cand) && dist[cand] == dist[cur] - 1) { prevFound = cand; found = true; }
                            var viaDoor = new Node(p.roomA, p.cellA);
                            if (!found && dist.ContainsKey(viaDoor) && dist[viaDoor] == dist[cur] - 1) { prevFound = viaDoor; found = true; }
                            break;
                        }
                        if (p.roomA == cur.room && p.cellA == cur.cell)
                        {
                            var cand = new Node(p.roomB, p.cellB);
                            if (dist.ContainsKey(cand) && dist[cand] == dist[cur] - 1) { prevFound = cand; found = true; }
                            break;
                        }
                    }
                }
                if (!found) break;
                cur = prevFound;
                path.Add(cur);
            }
            path.Reverse();
            return path;
        }
    }
}