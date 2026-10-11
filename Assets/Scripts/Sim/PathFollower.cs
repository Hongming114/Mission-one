using System.Collections.Generic;
using UnityEngine;
using DungeonCity.Gen;

namespace DungeonCity.Sim
{
    /// <summary>
    /// 沿房间路径跟到目标（史莱姆跨房间追人用）：
    /// 每 replanInterval 秒用 CellPathfinder 重算一次路径（它会自动把"门"当作传送到配对门格），
    /// 然后依次朝下一个路点走。找不到路径时返回 false，调用方退回"直线朝目标"。
    /// </summary>
    public sealed class PathFollower
    {
        public readonly float replanInterval;
        public readonly List<CellPathfinder.Node> path = new List<CellPathfinder.Node>();
        public int pathIndex;
        public float timer;
        public int replanCount;
        public bool avoidWorldPortals = true;    // 反馈第 5 条：史莱姆绝不允许踩梯子（III=2.2）
        Vector2Int lastGoalCell = new Vector2Int(int.MinValue, int.MinValue);

        public PathFollower(float interval) { replanInterval = Mathf.Max(0.05f, interval); }

        public static Vector2Int NodeWorldCell(WalkMap map, CellPathfinder.Node n)
        {
            Vector2Int wc;
            return map.TryWorldCell(n.room, n.cell, out wc) ? wc : new Vector2Int(int.MinValue, int.MinValue);
        }

        public bool TryStep(WalkMap map, WorldData world, Vector2 position, Vector2 target, float dt, out Vector2 dir)
        {
            dir = Vector2.zero;
            Vector2Int curCell = WalkMap.CellOf(position);
            Vector2Int goalCell = WalkMap.CellOf(target);
            RoomData cr, tr; Vector2Int cl, tl;
            if (!map.TryRoom(curCell, out cr, out cl)) return false;
            if (!map.TryRoom(goalCell, out tr, out tl)) return false;

            timer -= dt;
            bool stale = timer <= 0f || path.Count == 0 || pathIndex >= path.Count || goalCell != lastGoalCell;
            if (stale)
            {
                path.Clear();
                path.AddRange(CellPathfinder.Path(world, cr.grid, cl, tr.grid, tl));
                pathIndex = 0;
                while (pathIndex < path.Count - 1 && NodeWorldCell(map, path[pathIndex]) == curCell) pathIndex++;
                lastGoalCell = goalCell;
                timer = replanInterval;
                replanCount++;
            }
            if (path.Count == 0) return false;

            while (pathIndex < path.Count - 1)
            {
                Vector2Int nodeCell = NodeWorldCell(map, path[pathIndex]);
                // 跳过梯子格：史莱姆只能跟到梯子旁边，不能踩上去跨世界（反馈第 5 条）
                if (avoidWorldPortals && DungeonCity.Core.FuncCode.IsWorldPortal(map.FuncAt(nodeCell))) { pathIndex++; continue; }
                Vector2 wp = WalkMap.CenterOf(nodeCell);
                if (Vector2.Distance(position, wp) > 0.25f) break;
                pathIndex++;
            }

            Vector2 next = WalkMap.CenterOf(NodeWorldCell(map, path[pathIndex]));
            Vector2 d = next - position;
            if (d.sqrMagnitude < 1e-6f) d = target - position;
            if (d.sqrMagnitude < 1e-6f) return false;
            dir = d.normalized;
            return true;
        }
    }
}