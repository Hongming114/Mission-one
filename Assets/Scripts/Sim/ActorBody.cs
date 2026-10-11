using UnityEngine;

namespace DungeonCity.Sim
{
    /// <summary>
    /// 带碰撞体积的实体（连续坐标，单位 = 1 个方格边长）。
    /// 移动按轴分开、再分子步推进：撞墙就停在墙边，因此可以贴着墙滑行，不会穿墙。
    /// </summary>
    public class ActorBody
    {
        public Vector2 pos;
        public float radius = 0.3f;
        public Vector2Int facing = new Vector2Int(0, -1);
        public Vector2 moved = Vector2.zero;      // 本帧实际位移（动画用）
        public bool moving;
        public float portalCooldown;              // 刚穿完门的一小段冷却，避免来回触发
        public float substep = 0.02f;

        public bool Alive { get { return true; } }

        public float DistanceTo(ActorBody other) { return Vector2.Distance(pos, other.pos); }

        /// <summary>两个实体"贴在一起"时的间隙（负值表示已经重叠）。</summary>
        public float GapTo(ActorBody other)
        {
            return Vector2.Distance(pos, other.pos) - radius - other.radius;
        }

        /// <summary>尝试移动 delta（格）。返回本帧实际位移。</summary>
        public Vector2 Move(WalkMap map, Vector2 delta, bool forPlayer)
        {
            float bx = pos.x, by = pos.y;
            float step = substep <= 0f ? 0.02f : substep;
            if (delta.x != 0f) MoveAxis(map, new Vector2(delta.x, 0f), forPlayer, step);
            if (delta.y != 0f) MoveAxis(map, new Vector2(0f, delta.y), forPlayer, step);
            moved = new Vector2(pos.x - bx, pos.y - by);
            moving = moved.sqrMagnitude > 1e-8f;
            return moved;
        }

        void MoveAxis(WalkMap map, Vector2 delta, bool forPlayer, float step)
        {
            float remain = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
            Vector2 dir = new Vector2(delta.x == 0f ? 0f : Mathf.Sign(delta.x), delta.y == 0f ? 0f : Mathf.Sign(delta.y));
            while (remain > 1e-5f)
            {
                float s = Mathf.Min(step, remain);
                Vector2 tryPos = pos + dir * s;
                if (map.Blocked(tryPos, radius, forPlayer)) break;
                pos = tryPos;
                remain -= s;
            }
        }

        /// <summary>中心进入门格就穿门（送到配对门格内侧），并加一小段冷却避免来回触发。</summary>
        public bool ExitPortalIfAny(WalkMap map, bool forPlayer)
        {
            if (portalCooldown > 0f) return false;
            Vector2 exit;
            if (!map.TryExitPortal(pos, radius, forPlayer, out exit)) return false;
            pos = exit;
            portalCooldown = 0.25f;
            return true;
        }

        public void TickCooldowns(float dt)
        {
            if (portalCooldown > 0f) portalCooldown = Mathf.Max(0f, portalCooldown - dt);
        }

        /// <summary>把两个实体的碰撞体积推开（"不会相交、重合"）。推不动的一侧就由另一侧多让一点。</summary>
        public static void Separate(ActorBody a, ActorBody b, WalkMap map, bool aForPlayer, bool bForPlayer)
        {
            float min = a.radius + b.radius;
            for (int iter = 0; iter < 3; iter++)
            {
                Vector2 d = b.pos - a.pos;
                float dist = d.magnitude;
                if (dist >= min - 1e-4f) return;
                Vector2 n = dist > 1e-4f ? d / dist : new Vector2(0f, 1f);
                float penetration = min - Mathf.Max(dist, 1e-4f);
                Vector2 push = n * (penetration * 0.5f);
                Vector2 bNew = b.pos + push;
                if (!map.Blocked(bNew, b.radius, bForPlayer)) b.pos = bNew;
                Vector2 aNew = a.pos - push;
                if (!map.Blocked(aNew, a.radius, aForPlayer)) a.pos = aNew;
            }
        }
    }
}