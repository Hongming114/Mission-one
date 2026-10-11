using UnityEngine;

namespace DungeonCity.Sim
{
    /// <summary>
    /// 掉落物（文档 L43 / L44）：史莱姆死亡时在其死亡位置留下一个"史莱姆清"。
    ///   * 大小约 0.6 × 0.6 方格；
    ///   * 主角模型与它碰撞（圆心距 <= 主角半径 0.3 + 掉落半径 0.3）时触发背包的存放指令；
    ///   * 存不下（50 格已满 / 该物品到 99 上限）就终止存放指令，模型留在原地。
    /// 纯数据，画面由 SlimeDropView 负责。
    /// </summary>
    public class SlimeDrop
    {
        public const string ItemName = "史莱姆清";
        public const float SizeCells = 0.6f;
        public const float Radius = SizeCells * 0.5f;    // 0.3 格
        public const float PickupDistance = 0.6f;        // 主角 0.3 + 掉落 0.3
        public const float RetrySeconds = 0.8f;          // 存不下时提示的节流间隔

        public Vector2 pos;
        public int amount = 1;
        public bool picked;
        public float retryTimer;
        public string tag = ItemName;
    }
}
