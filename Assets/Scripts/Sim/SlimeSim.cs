using System.Collections.Generic;
using UnityEngine;
using DungeonCity.Core;
using DungeonCity.Gen;

namespace DungeonCity.Sim
{
    public enum SlimeState { Idle, Chase, Charge, Lunge, Cooldown, Hurt, Dead }

    /// <summary>
    /// 史莱姆逻辑（纯逻辑，不用 MonoBehaviour）：
    ///   * hp 20、atk 5、碰撞半径 0.4（约 0.8×0.8 方格）、追击速度 1 格/秒；
    ///   * 视野：以自身为圆心、3 个方格边长为半径（文档 L37：原 4，已对齐）；同一个房间内也算"知道主角在哪"；
    ///   * 反馈第 5 条：主角跑出房间/跑出视野后，史莱姆会记住"最后看到主角的位置"并跟过去
    ///     （房间图距离 <= ChaseRoomRange 才追），到地方找不到人就自己游荡（Roam）；
    ///   * 反馈第 5 条：没有视野也会自行移动、会自己穿门进别的房间；
    ///   * 反馈第 5 条：史莱姆绝不允许踩梯子（III=2.2）跨世界，路径与目标点都会绕开梯子格；
    ///   * 攻击：与主角的"间隙"（表面距离）≤ 0.5 格并持续 1 秒 -> 蓄力完成 -> 向主角扑击，
    ///     伤害由调用方结算（GameSession 会再做一次视线判定）；
    ///   * 受击（文档第 37 行）：终止自身攻击动作（清掉蓄力/扑击），建模变红，
    ///     并被击退 0.5 方格，hp 减少主角造成的伤害。
    /// 触发条件用"间隙"而不是圆心距：玩家半径 0.3 + 史莱姆 0.4 = 0.7，
    /// 在"碰撞体积不相交"的前提下圆心距 ≤ 0.5 永远不可能出现。
    /// </summary>
    public class SlimeSim : ActorBody
    {
        public const int HpDefault = 20;
        public const int AtkDefault = 5;
        public const float VisionRadius = 3f;                     // 文档 L37：视野 3 格（原 4）
        public const float MoveSpeed = 1f;
        public const float AttackGap = 0.5f;
        public const float ChargeSeconds = 1f;
        public const float LungeDuration = 0.4f;
        public const float LungeSpeed = 4f;
        public const float LungeCooldown = 0.8f;
        public const float HurtSeconds = 0.28f;
        public const float KnockbackDistance = 0.5f;
        public const float ReplanInterval = 0.25f;
        public const float ArriveDistance = 0.12f;

        // ---- 反馈第 5 条：跨房间追踪 / 自主游荡 / 不坐梯子 ----
        public const float MemorySeconds = 3f;         // 跟到"最后看到主角的位置"后最多再找 3 秒
        public const float MemoryArrive = 0.5f;        // 离记忆点这么近就算到了
        public const int ChaseRoomRange = 2;           // 自我审查第 4 条：房间图距离 > 2 的怪不追，避免全图一起冲
        public const float RoamSpeed = 0.7f;           // 自己溜达时比追击慢一点
        public const float RoamArrive = 0.6f;
        public const float RoamStuckSeconds = 1.2f;    // 自我审查第 9 条：卡住 1.2 秒就换个目标

        public int hp = HpDefault;
        public DungeonCity.Core.WorldKind worldKind = DungeonCity.Core.WorldKind.Dungeon;   // 这只怪属于哪个世界
        public bool lungeHit;                                                              // 本次扑击是否已经打到主角
        public int atk = AtkDefault;
        public SlimeState state = SlimeState.Idle;
        public float stateTimer;
        public float hurtFlash;
        public float stopTimer;                 // 停在原地计时（动画用）
        public int id;
        public string tag = "史莱姆";

        // ---- 追踪记忆 ----
        public bool hasMemory;                  // 是否记得"最后看到主角的位置"
        public Vector2 memoryPos;
        public float memoryTimer;               // 到达记忆点后开始计时，超时放弃
        public Vector2Int roomGrid = new Vector2Int(int.MinValue, int.MinValue);   // 当前所在房间（跨房间用）

        // ---- 自主游荡 ----
        public bool roaming;
        public Vector2 roamPos;
        public float roamPause;
        public float roamStuck;
        public bool pinnedToRoom;               // 调试3-1: 与主角同房间时为 true - 禁止任何跨房间移动

        public readonly PathFollower follow = new PathFollower(ReplanInterval);
        Vector2 knockDir = Vector2.zero;
        float knockRemain;
        public int hurtCount;
        public int hitPlayerCount;
        public int memoryRefreshCount;         // 记住主角位置的次数
        public int roamCount;                  // 自己选游荡目标的次数
        public int portalCount;                // 穿门次数（由 GameSession 结算时 +1）
        public int roomChangeCount;            // 换房间次数
        public readonly List<string> events = new List<string>();

        System.Random rng;
        Dictionary<Vector2Int, int> chaseDist;
        Vector2Int chaseDistRoot = new Vector2Int(int.MinValue, int.MinValue);

        public SlimeSim(int id)
        {
            this.id = id;
            radius = 0.4f;
            tag = $"史莱姆#{id}";
            rng = new System.Random(id * 7919 + 13);
            roamPause = (id % 5) * 0.35f;      // 自我审查第 6 条：生成时按 id 错开，别整齐划一
            follow.avoidWorldPortals = true;   // 反馈第 5 条：绝不允许坐梯子跨世界
        }

        public bool Dead { get { return state == SlimeState.Dead || hp <= 0; } }
        public bool InHurt { get { return state == SlimeState.Hurt; } }
        public bool Attacking { get { return state == SlimeState.Charge || state == SlimeState.Lunge; } }
        public bool Tracking { get { return hasMemory; } }
        public bool Awake { get { return state != SlimeState.Idle && state != SlimeState.Dead; } }
        public float ChargeProgress
        {
            get
            {
                if (state == SlimeState.Charge) return Mathf.Clamp01(1f - stateTimer / ChargeSeconds);
                return state == SlimeState.Lunge ? 1f : 0f;
            }
        }

        public float AttackGapTo(PlayerSim p)
        {
            return Vector2.Distance(pos, p.pos) - radius - p.radius;
        }

        public bool Sees(PlayerSim p)
        {
            return p != null && !p.Dead && Vector2.Distance(pos, p.pos) <= VisionRadius;
        }

        /// <summary>两只实体是否在同一间房间里（房间级感知：主角跑出视野但没出房间也算知道）。</summary>
        public bool SameRoomAs(WalkMap map, Vector2 other)
        {
            RoomData a, b;
            Vector2Int la, lb;
            if (!map.TryRoomOf(pos, out a, out la)) return false;
            if (!map.TryRoomOf(other, out b, out lb)) return false;
            return a.grid == b.grid;
        }

        public void Tick(float dt, WalkMap map, WorldData world, PlayerSim player)
        {
            Tick(dt, map, world, player, true);
        }

        /// <param name="playerInWorld">主角是否和这只史莱姆在同一个世界（主角回城市时为 false：只按记忆/游荡行动）。</param>
        public void Tick(float dt, WalkMap map, WorldData world, PlayerSim player, bool playerInWorld)
        {
            TickCooldowns(dt);
            if (hurtFlash > 0f) hurtFlash = Mathf.Max(0f, hurtFlash - dt);

            if (state == SlimeState.Dead) { moving = false; return; }

            bool alive = player != null && !player.Dead && playerInWorld;
            bool sees = alive && Sees(player);
            bool sameRoom = alive && SameRoomAs(map, player.pos);
            bool aware = sees || sameRoom;
            pinnedToRoom = sameRoom;           // 调试3-1 同房间禁止跨房间
            if (aware) RememberPlayer(map, world, player.pos);

            if (state == SlimeState.Hurt)
            {
                float step = knockRemain > 1e-4f
                    ? Mathf.Min(knockRemain, KnockbackDistance * (dt / Mathf.Max(HurtSeconds, 1e-4f)))
                    : 0f;
                knockRemain = Mathf.Max(0f, knockRemain - step);
                Move(map, knockDir * step, false);
                stateTimer = Mathf.Max(0f, stateTimer - dt);
                if (stateTimer <= 0f) state = (aware || hasMemory) ? SlimeState.Chase : SlimeState.Idle;
                NoteRoomChange(map);
                return;
            }

            if (state == SlimeState.Lunge)
            {
                stateTimer = Mathf.Max(0f, stateTimer - dt);
                Vector2 aim = aware ? player.pos : (hasMemory ? memoryPos : pos + new Vector2(facing.x, facing.y));
                Vector2 toward = aim - pos;
                if (toward.sqrMagnitude > 1e-6f) facing = MoveInput.AxisDir(toward);
                Move(map, toward.normalized * (LungeSpeed * dt), false);
                if (stateTimer <= 0f) { state = SlimeState.Cooldown; stateTimer = LungeCooldown; }
                NoteRoomChange(map);
                return;
            }

            if (state == SlimeState.Cooldown)
            {
                stateTimer = Mathf.Max(0f, stateTimer - dt);
                moving = false;
                if (stateTimer <= 0f) state = (aware || hasMemory) ? SlimeState.Chase : SlimeState.Idle;
                return;
            }

            if (aware)
            {
                stopTimer = 0f;
                Vector2 toPlayer = player.pos - pos;
                if (toPlayer.sqrMagnitude > 1e-6f) facing = MoveInput.AxisDir(toPlayer);
                float gap = AttackGapTo(player);

                // 隔墙不能隔空蓄力（自我审查第 3 条）
                bool lineOk = LineOfSight.Clear(map, pos, player.pos, true);
                if (state != SlimeState.Charge && gap <= AttackGap && lineOk)
                {
                    state = SlimeState.Charge;
                    stateTimer = ChargeSeconds;
                    Note($"贴住主角（间隙 {gap:0.00} 格）开始蓄力，1 秒后扑击");
                }

                if (state == SlimeState.Charge)
                {
                    stateTimer = Mathf.Max(0f, stateTimer - dt);
                    if (gap > AttackGap || !lineOk)
                    {
                        state = SlimeState.Chase;                 // 主角跑出扑击范围/被墙挡住，蓄力中断（按条件重新计时）
                        Note("蓄力中断（主角跑开或被墙挡住）");
                    }
                    else if (stateTimer <= 0f)
                    {
                        state = SlimeState.Lunge;
                        stateTimer = LungeDuration;
                        Note("扑击！");
                    }
                    NoteRoomChange(map);
                    return;
                }

                // 追击：朝主角移动（沿房间路径，允许穿门跨房间），目标点绕开梯子格
                state = SlimeState.Chase;
                MoveToward(map, world, ClampTarget(map, player.pos), dt, MoveSpeed);
                NoteRoomChange(map);
                return;
            }

            // 看不到主角：如果还记得"最后看到的位置"就过去看看
            if (hasMemory)
            {
                float dm = Vector2.Distance(pos, memoryPos);
                if (dm <= MemoryArrive) memoryTimer += dt;
                if (memoryTimer >= MemorySeconds)
                {
                    hasMemory = false;
                    memoryTimer = 0f;
                    Note("在最后看到主角的位置没找到人，改为自行游荡");
                }
                else
                {
                    state = SlimeState.Chase;
                    stopTimer = 0f;
                    Vector2 toward = memoryPos - pos;
                    if (toward.sqrMagnitude > 1e-6f) facing = MoveInput.AxisDir(toward);
                    if (dm > ArriveDistance) MoveToward(map, world, memoryPos, dt, MoveSpeed);
                    else moving = false;
                    NoteRoomChange(map);
                    return;
                }
            }

            Roam(dt, map, world);
            NoteRoomChange(map);
        }

        /// <summary>主角坐梯子离开地牢时调用：把"主角离开的位置"写进记忆，怪会跟到梯子旁边（但绝不踩上去）。</summary>
        public void SetMemoryTo(WalkMap map, WorldData world, Vector2Int worldCell)
        {
            if (Dead) return;
            Vector2 p = ClampTarget(map, WalkMap.CenterOf(worldCell));
            if (!WithinChaseRange(map, world, p)) return;
            memoryPos = p;
            hasMemory = true;
            memoryTimer = 0f;
            memoryRefreshCount++;
            Note($"记住了主角离开的位置（格 {worldCell.x},{worldCell.y}），跟过去（不坐梯子）");
        }

        // ---------------- 内部 ----------------

        void RememberPlayer(WalkMap map, WorldData world, Vector2 playerPos)
        {
            if (!WithinChaseRange(map, world, playerPos)) return;
            Vector2 clamped = ClampTarget(map, playerPos);
            bool first = !hasMemory;
            memoryPos = clamped;
            hasMemory = true;
            memoryTimer = 0f;
            memoryRefreshCount++;
            if (first) Note("锁定主角位置（记忆）");
        }

        /// <summary>房间图距离太远就不追（自我审查第 4 条：避免全图史莱姆一起冲过来）。</summary>
        bool WithinChaseRange(WalkMap map, WorldData world, Vector2 target)
        {
            RoomData a, b;
            Vector2Int la, lb;
            if (!map.TryRoomOf(pos, out a, out la)) return true;
            if (!map.TryRoomOf(target, out b, out lb)) return true;
            if (a.grid == b.grid) return true;
            if (chaseDist == null || chaseDistRoot != b.grid)
            {
                chaseDist = RoomDistanceField.Distances(world, b.grid);
                chaseDistRoot = b.grid;
            }
            int d;
            if (chaseDist == null || !chaseDist.TryGetValue(a.grid, out d)) return true;
            return d >= 0 && d <= ChaseRoomRange;
        }

        /// <summary>目标点落在梯子格上时，改成同一间房间里离它最近的可行走格（反馈第 5 条：不坐梯子）。</summary>
        public Vector2 ClampTarget(WalkMap map, Vector2 raw)
        {
            Vector2Int cell = WalkMap.CellOf(raw);
            if (!FuncCode.IsWorldPortal(map.FuncAt(cell))) return raw;
            RoomData room;
            Vector2Int local;
            Vector2 center = WalkMap.CenterOf(cell);
            if (!map.TryRoomOf(center, out room, out local)) return center;
            Vector2 best = center;
            float bestD = float.MaxValue;
            bool found = false;
            for (int y = 0; y < room.height; y++)
            {
                for (int x = 0; x < room.width; x++)
                {
                    Vector2Int wc;
                    if (!map.TryWorldCell(room.grid, new Vector2Int(x, y), out wc)) continue;
                    if (!map.IsWalkable(wc, false)) continue;      // 对怪可走（梯子格自然被排除）
                    float d = Vector2.Distance(WalkMap.CenterOf(wc), center);
                    if (!found || d < bestD) { bestD = d; best = WalkMap.CenterOf(wc); found = true; }
                }
            }
            return best;
        }

        void MoveToward(WalkMap map, WorldData world, Vector2 target, float dt, float speed)
        {
            Vector2 dir;
            if (follow.TryStep(map, world, pos, target, dt, out dir)) dir = dir.normalized;
            else
            {
                Vector2 t = target - pos;
                dir = t.sqrMagnitude > 1e-6f ? t.normalized : Vector2.zero;
            }
            Move(map, dir * (speed * dt), false);
        }

        void Roam(float dt, WalkMap map, WorldData world)
        {
            state = SlimeState.Idle;
            stopTimer += dt;
            if (roamPause > 0f) { roamPause = Mathf.Max(0f, roamPause - dt); moving = false; return; }
            if (!roaming && !PickRoamTarget(map, world)) { moving = false; return; }

            float d = Vector2.Distance(pos, roamPos);
            if (d <= RoamArrive)
            {
                roaming = false;
                roamPause = 0.4f + (float)rng.NextDouble() * 1.4f;   // 到点后停 0.4~1.8 秒再选新点
                moving = false;
                return;
            }

            Vector2 before = pos;
            MoveToward(map, world, roamPos, dt, RoamSpeed);
            if (Vector2.Distance(before, pos) < RoamSpeed * dt * 0.25f)
            {
                roamStuck += dt;
                if (roamStuck >= RoamStuckSeconds)
                {
                    roaming = false;
                    roamPause = 0.3f;
                    roamStuck = 0f;
                    Note("游荡被卡住，换个目标");
                }
            }
            else roamStuck = 0f;
        }

        bool PickRoamTarget(WalkMap map, WorldData world)
        {
            for (int attempt = 0; attempt < 24; attempt++)
            {
                Vector2Int key;
                // 调试3-1 同房间只在本房间挑点
                if (pinnedToRoom || attempt >= 16 || rng.NextDouble() < 0.7)
                {
                    RoomData cur;
                    Vector2Int local;
                    if (!map.TryRoomOf(pos, out cur, out local)) return false;
                    key = cur.grid;                                  // 70% 概率就在当前房间挑
                }
                else
                {
                    List<Vector2Int> keys = WorldGenerator.SortedKeys(world);
                    if (keys == null || keys.Count == 0) return false;
                    key = keys[rng.Next(keys.Count)];                // 30% 概率挑别的房间（会自己走门过去）
                }
                RoomData room = world.Room(key);
                if (room == null) continue;
                Vector2Int wc;
                Vector2Int local2 = new Vector2Int(rng.Next(room.width), rng.Next(room.height));
                if (!map.TryWorldCell(key, local2, out wc)) continue;
                if (!map.IsWalkable(wc, false)) continue;
                roamPos = WalkMap.CenterOf(wc);
                roaming = true;
                roamStuck = 0f;
                roamCount++;
                return true;
            }
            return false;
        }

        void NoteRoomChange(WalkMap map)
        {
            RoomData room;
            Vector2Int local;
            if (!map.TryRoomOf(pos, out room, out local)) return;
            if (roomGrid == room.grid) return;
            if (roomGrid.x > int.MinValue)
            {
                roomChangeCount++;
                Note($"移动到房间({room.grid.x},{room.grid.y})");
            }
            roomGrid = room.grid;
        }

        /// <summary>
        /// 受到主角攻击：终止自身攻击动作 + 变红（受击状态）+ 沿 fromDirection 方向击退 0.5 格 + 掉血。
        /// 返回本次是否致命。
        /// </summary>
        public bool TakeHit(int damage, Vector2 fromDirection)
        {
            if (Dead) return true;
            state = SlimeState.Hurt;      // 终止自身攻击动作（蓄力/扑击被打断）
            lungeHit = false;
            stateTimer = HurtSeconds;
            hurtFlash = HurtSeconds;
            hurtCount++;
            knockDir = fromDirection.sqrMagnitude > 1e-6f ? fromDirection.normalized : new Vector2(facing.x, facing.y);
            knockRemain = KnockbackDistance;
            hp = Mathf.Max(0, hp - damage);
            Note($"受击：-{damage} HP（{hp}/{HpDefault}）、变红、被击退 0.5 格、攻击动作终止");
            if (hp <= 0)
            {
                state = SlimeState.Dead;
                Note("被击杀");
                return true;
            }
            return false;
        }

        public void Note(string s)
        {
            events.Add(s);
            if (events.Count > 30) events.RemoveAt(0);
        }

        public string LastEvent { get { return events.Count == 0 ? "" : events[events.Count - 1]; } }
    }
}