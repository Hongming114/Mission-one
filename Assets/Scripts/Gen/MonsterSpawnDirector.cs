using UnityEngine;
using DungeonCity.Core;

namespace DungeonCity.Gen
{
    /// <summary>
    /// 刷怪节奏控制（文档规定的时间规则，只有数据与计时，不生成怪物实体）：
    ///   - 每经过 t = 1min 判定一次；
    ///   - 场上怪数量 &lt;= 2 时"立即进行判定"（不用等满 1min，但设了一个最小冷却避免每帧判定）；
    ///   - 场上怪数量 &gt;= 2 * 房间数 时仍然判定，只是判定成功也不真的生成（由 MonsterSpawnPlanner 负责）。
    /// 怪物实体与 AI 由后续提示词接入，这里先把判定时刻与结果记录下来，便于 HUD 展示与验证。
    /// </summary>
    public class MonsterSpawnDirector
    {
        public WorldGenConfig config;
        public float timer;              // 距离上次判定的时间
        public int judgeCount;           // 已经判定了多少次
        public MonsterSpawnPlan lastPlan;
        public bool lastJudgeWasForced;  // 上一次判定是否属于"怪太少，立即判定"

        public MonsterSpawnDirector(WorldGenConfig config)
        {
            this.config = config;
        }

        public float Interval => config.monsterJudgeIntervalSeconds;

        /// <summary>下一次判定还要等多少秒（仅用于 HUD 显示）。</summary>
        public float TimeToNextJudge(int currentMonsterCount)
        {
            if (currentMonsterCount <= config.monsterForceJudgeMaxCount) return 0f;
            return Mathf.Max(0f, Interval - timer);
        }

        public bool Capped(int currentMonsterCount, WorldData world)
            => currentMonsterCount >= config.monsterCapRoomsFactor * world.RoomCount;

        /// <summary>
        /// 每帧调用一次。到了该判定的时刻就返回这次的判定结果，否则返回 null。
        /// </summary>
        public MonsterSpawnPlan Tick(float deltaTime, WorldData world, Vector2Int playerRoom, int currentMonsterCount)
        {
            timer += deltaTime;
            bool tooFew = currentMonsterCount <= config.monsterForceJudgeMaxCount;
            bool due = tooFew
                ? timer >= config.monsterForceJudgeMinInterval
                : timer >= config.monsterJudgeIntervalSeconds;
            if (!due) return null;

            timer = 0f;
            judgeCount++;
            lastJudgeWasForced = tooFew;
            lastPlan = MonsterSpawnPlanner.Plan(world, playerRoom, currentMonsterCount, world.seed + judgeCount * 977);
            return lastPlan;
        }

        /// <summary>切世界/重新开始时重置计时。</summary>
        public void ResetTimer()
        {
            timer = 0f;
        }

        public string Describe(int currentMonsterCount, WorldData world)
        {
            string s = $"刷怪判定：周期 t={Interval:0}s，已判定 {judgeCount} 次，下次判定还需 {TimeToNextJudge(currentMonsterCount):0.#}s";
            if (currentMonsterCount <= config.monsterForceJudgeMaxCount) s += "（怪 <= 2：立即判定）";
            if (Capped(currentMonsterCount, world)) s += "【已达上限 2 x 房间数：判定成功也不生成】";
            return s;
        }
    }
}