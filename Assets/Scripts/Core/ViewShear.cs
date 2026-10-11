using UnityEngine;

namespace DungeonCity.Core
{
    /// <summary>
    /// 调试4（遮挡）：把有高度的顶点沿相机视线方向压平。
    /// 正交相机 pitch=52°、yaw=0 时 forward=(0,-0.788,0.616)。沿视线平移在正交投影下不改变屏幕位置，
    /// 但让深度只与水平位置有关：墙在南侧（相机与玩家之间）才会遮住玩家，
    /// 墙在北侧（玩家身后）永远不会在角色身上切出横线。
    /// </summary>
    public static class ViewShear
    {
        public const float DefaultPitchDeg = 52f;
        public static bool Enabled = true;
        public static float PitchDeg = DefaultPitchDeg;

        public static Vector3 Flatten(Vector3 v)
        {
            if (!Enabled) return v;
            float rad = PitchDeg * Mathf.Deg2Rad;
            float s = Mathf.Sin(rad);
            float c = Mathf.Cos(rad);
            return new Vector3(v.x, v.y * (1f - s * s), v.z + c * s * v.y);
        }

        /// <summary>相机深度（视线方向的分量）。压平后只与水平位置有关，用于验证遮挡关系。</summary>
        public static float Depth(Vector3 v)
        {
            float rad = PitchDeg * Mathf.Deg2Rad;
            return -Mathf.Sin(rad) * v.y + Mathf.Cos(rad) * v.z;
        }

        /// <summary>屏幕纵坐标（相机 up 方向的分量）。压平前后必须一致。</summary>
        public static float ScreenUp(Vector3 v)
        {
            float rad = PitchDeg * Mathf.Deg2Rad;
            return Mathf.Cos(rad) * v.y + Mathf.Sin(rad) * v.z;
        }
    }
}
