using UnityEngine;

namespace DungeonCity.Sim
{
    /// <summary>
    /// 视线判定（自我审查第 3 条）：隔墙不该砍到怪、怪也不该隔墙扑到主角。
    /// 做法：沿两点连线等距采样，用很小的探测半径查 WalkMap.Blocked；
    /// 两端各跳过 EndpointSkip（不然贴着墙站位会自己挡自己）。
    /// </summary>
    public static class LineOfSight
    {
        public const float SampleStep = 0.1f;
        public const float EndpointSkip = 0.55f;
        public const float ProbeRadius = 0.08f;

        /// <summary>两点之间是否没有墙。forPlayer 决定梯子格算不算阻挡（默认按可走算）。</summary>
        public static bool Clear(WalkMap map, Vector2 a, Vector2 b, bool forPlayer = true)
        {
            if (map == null) return true;
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len <= EndpointSkip * 2f) return true;
            Vector2 dir = d / len;
            float end = len - EndpointSkip;
            for (float t = EndpointSkip; t < end; t += SampleStep)
            {
                if (map.Blocked(a + dir * t, ProbeRadius, forPlayer)) return false;
            }
            return true;
        }
    }
}