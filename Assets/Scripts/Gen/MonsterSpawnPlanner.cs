using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DungeonCity.Core;

namespace DungeonCity.Gen
{
    public struct MonsterSpawnPoint
    {
        public Vector2Int room;
        public Vector2Int cell;
        public int roomDistance;
    }

    /// <summary>一次刷怪判定的结果（只有数据；怪物实体与 AI 见后续提示词）。</summary>
    public class MonsterSpawnPlan
    {
        public WorldKind kind;
        public int seed;
        public Vector2Int playerRoom;
        public float p;                     // 每个被抽中格子的生成概率 P
        public float cellSampleRatio;       // 每个房间抽样的格子比例（10%）
        public int minRoomDistance;         // 只刷距离 >= 这个值的房间
        public int judgeIntervalSeconds;    // 判定周期 t
        public int currentMonsterCount;     // 判定时场上已有的怪
        public int capTotal;                // 上限 = monsterCapRoomsFactor * 房间数
        public bool capped;                 // 是否已达上限（判定照做，但成功也不生成）
        public int eligibleRooms;           // 满足距离条件的房间数
        public int sampledCells;            // 一共抽了多少个格子
        public int rolledSuccesses;         // 抽中且概率判定通过的次数（含被上限压制的）
        public int suppressedByCap;         // 因为上限而没有真的生成的
        public readonly List<MonsterSpawnPoint> points = new List<MonsterSpawnPoint>();

        public int MonsterCount => points.Count;
        public int JudgeIntervalSeconds => judgeIntervalSeconds;

        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"刷怪判定 M={(int)kind}({kind}) 玩家房间={playerRoom} 种子={seed}");
            sb.AppendLine($"规则：距离 >= {minRoomDistance} 的房间，各抽 {cellSampleRatio * 100f:0.#}%（向下取整）的格子，每格概率 P={p:0.###}；判定周期 t={judgeIntervalSeconds}s");
            sb.AppendLine($"当前怪数 {currentMonsterCount} / 上限 {capTotal}（= {2} x 房间数）" + (capped ? "  【已达上限：判定继续，但成功也不生成】" : ""));
            sb.AppendLine($"满足距离条件的房间 {eligibleRooms} 个，抽样格子 {sampledCells} 个，判定成功 {rolledSuccesses} 次，被上限压制 {suppressedByCap} 次");
            sb.AppendLine($"本次实际生成 {points.Count} 只：");
            foreach (MonsterSpawnPoint pt in points)
                sb.AppendLine($"  怪 @ 房间({pt.room.x},{pt.room.y}) 格({pt.cell.x},{pt.cell.y}) 距离={pt.roomDistance}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 刷怪规则（本阶段只做规则与数据，不做怪物实体/AI）：
    /// 1) 距离 = 离玩家所在房间至少要经过的房间数（玩家房间 = 0）；
    /// 2) 对每个"距离 >= 2"的房间，随机抽 10%（向下取整）的格子（抽样范围是 III != 0 的格子）；
    /// 3) 每个抽中的格子以较低概率 P 生成一只怪（只落在 III = 1 普通可达格子上，门/梯子这类功能格不站怪）；
    /// 4) 场上怪 >= 2 * 房间数 时判定照做，但即使成功也不生成。
    /// TODO(后续提示词)：怪物实体、跨房间移动、追踪、视野判定、攻击。
    /// </summary>
    public static class MonsterSpawnPlanner
    {
        public static MonsterSpawnPlan Plan(WorldData world, Vector2Int playerRoom, int currentMonsterCount, int? overrideSeed = null)
        {
            WorldGenConfig cfg = WorldCatalog.For(world.kind);
            return Plan(world, playerRoom, currentMonsterCount, cfg, overrideSeed ?? world.seed + 977);
        }

        public static MonsterSpawnPlan Plan(WorldData world, Vector2Int playerRoom, int currentMonsterCount, WorldGenConfig cfg, int seed)
        {
            var plan = new MonsterSpawnPlan
            {
                kind = world.kind,
                seed = seed,
                playerRoom = playerRoom,
                p = cfg.monsterSpawnChanceP,
                cellSampleRatio = cfg.monsterCellSampleRatio,
                minRoomDistance = cfg.monsterMinRoomDistance,
                judgeIntervalSeconds = Mathf.RoundToInt(cfg.monsterJudgeIntervalSeconds),
                currentMonsterCount = currentMonsterCount,
                capTotal = cfg.monsterCapRoomsFactor * world.RoomCount,
            };
            plan.capped = currentMonsterCount >= plan.capTotal;

            if (world.kind != WorldKind.Dungeon) return plan;   // 只有地牢刷怪

            Dictionary<Vector2Int, int> dist = RoomDistanceField.Distances(world, playerRoom);
            var rng = new System.Random(seed);

            foreach (Vector2Int key in WorldGenerator.SortedKeys(world))
            {
                int d;
                if (!dist.TryGetValue(key, out d)) continue;                 // 与玩家不连通
                if (d < cfg.monsterMinRoomDistance) continue;
                plan.eligibleRooms++;

                RoomData room = world.Room(key);
                List<Vector2Int> pool = SamplePool(room);
                int sampleCount = Mathf.FloorToInt(pool.Count * cfg.monsterCellSampleRatio);   // 10%，向下取整
                Shuffle(pool, rng);

                for (int i = 0; i < sampleCount; i++)
                {
                    Vector2Int cell = pool[i];
                    plan.sampledCells++;
                    if (rng.NextDouble() >= cfg.monsterSpawnChanceP) continue;   // 概率判定
                    plan.rolledSuccesses++;
                    if (!FuncCode.IsWalkable(room.Func(cell.x, cell.y))) continue;  // 门/梯子：判定成功也不站怪
                    if (plan.capped) { plan.suppressedByCap++; continue; }          // 达上限：判定照做但不生成
                    plan.points.Add(new MonsterSpawnPoint
                    {
                        room = key,
                        cell = cell,
                        roomDistance = d,
                    });
                }
            }
            return plan;
        }

        /// <summary>抽样池：房间里所有 III != 0（非不可达）的格子。</summary>
        private static List<Vector2Int> SamplePool(RoomData room)
        {
            var pool = new List<Vector2Int>(room.width * room.height);
            for (int y = 0; y < room.height; y++)
                for (int x = 0; x < room.width; x++)
                    if (!FuncCode.IsBlocked(room.func[x, y])) pool.Add(new Vector2Int(x, y));
            return pool;
        }

        private static void Shuffle(List<Vector2Int> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                Vector2Int t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }
    }
}