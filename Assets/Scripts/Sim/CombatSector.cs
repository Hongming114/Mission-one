using UnityEngine;

namespace DungeonCity.Sim
{
    /// <summary>
    /// 挥剑的扇形判定：以 origin 为圆心、radius 为半径、朝 facing 方向左右各 halfAngleDeg 的扇形。
    /// 目标本身有体积（targetRadius），所以判定时张角放宽 asin(targetRadius / dist)，"擦到边"也算命中。
    /// </summary>
    public static class CombatSector
    {
        public static float AngleDeg(Vector2 v) { return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg; }

        /// <summary>点相对"面向"的角度差（度，右为正、左为负，范围 -180..180）。</summary>
        public static float RelativeDeg(Vector2Int facing, Vector2 origin, Vector2 point)
        {
            float face = AngleDeg(new Vector2(facing.x, facing.y));
            float ang = AngleDeg(point - origin);
            return Mathf.DeltaAngle(face, ang);
        }

        public static bool Contains(Vector2 origin, Vector2Int facing, float radius, float halfAngleDeg,
                                    Vector2 target, float targetRadius)
        {
            Vector2 d = target - origin;
            float dist = d.magnitude;
            if (dist > radius + targetRadius) return false;
            if (dist <= targetRadius + 1e-4f) return true;
            float rel = RelativeDeg(facing, origin, target);
            float slack = Mathf.Asin(Mathf.Clamp01(targetRadius / dist)) * Mathf.Rad2Deg;
            return Mathf.Abs(rel) <= halfAngleDeg + slack;
        }

        /// <summary>扇形内的所有实体（目标体积也算进去）。</summary>
        public static System.Collections.Generic.List<T> Inside<T>(System.Collections.Generic.List<T> bodies,
            Vector2 origin, Vector2Int facing, float radius, float halfAngleDeg) where T : ActorBody
        {
            var hit = new System.Collections.Generic.List<T>();
            for (int i = 0; i < bodies.Count; i++)
            {
                T b = bodies[i];
                if (b == null) continue;
                if (Contains(origin, facing, radius, halfAngleDeg, b.pos, b.radius)) hit.Add(b);
            }
            return hit;
        }
    }
}