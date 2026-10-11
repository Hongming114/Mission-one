namespace DungeonCity.Core
{
    /// <summary>
    /// 相机缩放模型（纯数学，放在 Core 里以便在没有 Unity 的逻辑台架中验证）。
    /// 调试2-2：鼠标滚轮调换视角——100% = 调试1 之后的默认视角（orthographicSize = 11 / 1.75），
    /// 上滑最多放大到 200%，下滑最多缩小到 50%。
    /// </summary>
    public static class CameraZoom
    {
        public const float MinPercent = 50f;
        public const float MaxPercent = 200f;
        public const float DefaultPercent = 100f;
        /// <summary>滚轮一格（Input.mouseScrollDelta.y = ±1）= 25%。</summary>
        public const float WheelStep = 25f;

        public static float Clamp(float percent)
        {
            if (percent < MinPercent) return MinPercent;
            if (percent > MaxPercent) return MaxPercent;
            return percent;
        }

        /// <summary>滚轮上滑（scrollDelta &gt; 0）放大，下滑（scrollDelta &lt; 0）缩小。</summary>
        public static float ApplyScroll(float percent, float scrollDelta)
        {
            if (scrollDelta == 0f) return Clamp(percent);
            float dir = scrollDelta > 0f ? 1f : -1f;
            return Clamp(percent + dir * WheelStep);
        }

        /// <summary>百分比 → 正交相机尺寸（百分比越大，orthographicSize 越小 = 画面越放大）。</summary>
        public static float OrthoSize(float baseOrthoSize, float percent)
        {
            float p = Clamp(percent);
            if (p <= 0f) p = DefaultPercent;
            return baseOrthoSize * DefaultPercent / p;
        }
    }
}