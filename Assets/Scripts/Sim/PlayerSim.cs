using System.Collections.Generic;
using UnityEngine;

namespace DungeonCity.Sim
{
    /// <summary>
    /// 主角逻辑（纯逻辑，不用 MonoBehaviour）：
    ///   * HP 100、ATK 10；
    ///   * 八向移动，速度 3 格/秒（调试2-3：×1.5）；收到新的移动指令才改朝向，否则朝向不变；
    ///   * Shift 冲刺：0.5 秒移动 1.5 格（3 格/秒），有残影、期间免疫伤害，结束后 0.8 秒冷却；
    ///     冲刺方向 = 当前移动指令方向，没有指令时朝面向方向；
    ///   * 鼠标左键挥剑：0.4 秒逆时针扫过 180°，伤害判定为"以自身为圆心、剑长 1 格为半径、
    ///     朝面向方向圆心角 150° 的扇形"，伤害 = ATK；挥剑过程中仍然可以移动。
    ///   * 空格跳跃（文档 L31~L33）：0.75 秒升 1.5 格、再 0.75 秒落回，空中仍可水平移动（跳跃与行走的合运动）。
    /// 视图层只读这里的字段做动画，不反向修改逻辑。
    /// </summary>
    public class PlayerSim : ActorBody
    {
        public const int MaxHpDefault = 100;
        public const int AtkDefault = 10;
        public const float MoveSpeed = 3f;                              // 调试2-3：走速 ×1.5（原 2 格/秒 -> 3 格/秒）
        public const float DashDistance = 1.5f;
        public const float DashDuration = 0.5f;
        public const float DashSpeedMultiplier = 1.5f;                 // 调试2-3：走速变快后同步提高，保持冲刺明显更快（0.5s 约 2.25 格）
        public const float DashSpeed = DashDistance / DashDuration * DashSpeedMultiplier;  // 调试2-3：4.5 格/秒 -> 0.5s 跑 2.25 格
        public const float HurtInvulSeconds = 0.6f;                     // 自我审查第 2 条：受伤后短暂无敌帧，避免被多只怪连锁秒杀
        public const float DashCooldown = 0.8f;
        public const float AttackDuration = 0.4f;                     // 文档 L30：挥剑 0.4 秒（原 0.5）
        public const float AttackCooldown = 0.08f;                      // 上次挥剑结束到能再次挥剑的最小间隔
        public const float SwordLength = 1f;                            // 剑长 = 1 个方格边长
        public const float SwordHalfAngleDeg = 75f;                     // 圆心角 150° -> 左右各 75°
        public const float HurtFlashSeconds = 0.35f;

        // 文档 L31~L33：跳跃（空格）——朝"垂直与地面向上"移动 1.5 格，再落回，两次各 0.75 秒
        public const float JumpRiseSeconds = 0.75f;
        public const float JumpFallSeconds = 0.75f;
        public const float JumpHeight = 0.75f;   // 调试3-2：跳跃高度改为原来的 50%（1.5 -> 0.75 格）
        public const float JumpTotalSeconds = JumpRiseSeconds + JumpFallSeconds;

        public int hp = MaxHpDefault;
        public int hpMax = MaxHpDefault;
        public int atk = AtkDefault;
        public Vector2Int inputDir = MoveInput.None;

        public float dashTimer, dashCooldown, attackTimer, attackCooldown, hurtFlash, hurtInvul;
        public Vector2 dashDir = new Vector2(0f, -1f);
        public float dashFade;                                          // >0 表示"刚起步"（给视图做冲击波特效）
        public int dashBlockedCount;                                    // 冲刺撞墙提前结束的次数（调试/HUD 用）
        public float sweepPrevRel = -90f, sweepRel = -90f;
        public int hitCount, killCount, hurtCount, dashCount, swingCount;
        public float jumpTimer = -1f;                                   // >=0 表示浮空中（0 = 起跳瞬间，落地回到 -1）
        public int jumpCount;
        public readonly List<string> events = new List<string>();

        public bool Dashing { get { return dashTimer > 0f; } }
        public bool Attacking { get { return attackTimer > 0f; } }
        public bool Invulnerable { get { return Dashing; } }            // 冲刺过程免疫伤害
        public bool HurtImmune { get { return Dashing || hurtInvul > 0f; } }   // 冲刺 + 受伤无敌帧
        public bool CanDash { get { return dashTimer <= 0f && dashCooldown <= 0f; } }
        public bool CanAttack { get { return attackTimer <= 0f && attackCooldown <= 0f; } }
        public bool Dead { get { return hp <= 0; } }
        public float DashCooldownLeft { get { return Mathf.Max(0f, dashCooldown); } }
        public float AttackProgress { get { return Attacking ? Mathf.Clamp01(1f - attackTimer / AttackDuration) : 0f; } }
        /// <summary>剑刃相对面向的角度：-90° -> +90°（逆时针扫过 180°）。</summary>
        public float BladeRelativeDeg { get { return -90f + 180f * AttackProgress; } }

        // 文档 L31~L33：跳跃
        public bool Jumping { get { return jumpTimer >= 0f; } }
        public bool CanJump { get { return jumpTimer < 0f; } }
        /// <summary>浮空高度（格）：0.75s 升到 1.5 格、再 0.75s 落回地面（重力抛物线）。</summary>
        public float AirHeight
        {
            get
            {
                if (jumpTimer < 0f) return 0f;
                float x = (jumpTimer - JumpRiseSeconds) / JumpRiseSeconds;   // -1 .. +1
                return JumpHeight * Mathf.Max(0f, 1f - x * x);
            }
        }

        public void Tick(float dt, WalkMap map, Vector2Int newInputDir, bool dashRequested, bool attackRequested, bool jumpRequested = false)
        {
            TickCooldowns(dt);
            if (dashCooldown > 0f) dashCooldown = Mathf.Max(0f, dashCooldown - dt);
            if (attackCooldown > 0f) attackCooldown = Mathf.Max(0f, attackCooldown - dt);
            if (dashFade > 0f) dashFade = Mathf.Max(0f, dashFade - dt);
            if (hurtFlash > 0f) hurtFlash = Mathf.Max(0f, hurtFlash - dt);
            if (hurtInvul > 0f) hurtInvul = Mathf.Max(0f, hurtInvul - dt);

            // 收到新的移动指令：更新朝向（在收到新的移动指令前不会改变）
            if (!MoveInput.IsNone(newInputDir))
            {
                inputDir = newInputDir;
                facing = newInputDir;
            }

            // 冲刺
            if (dashRequested && CanDash)
            {
                dashTimer = DashDuration;
                dashCooldown = DashDuration + DashCooldown;
                dashDir = MoveInput.IsNone(inputDir) ? new Vector2(facing.x, facing.y) : MoveInput.ToVector(inputDir);
                dashFade = 0.30f;   // 起步冲击波时长（视图层用）
                dashCount++;
                Note($"冲刺（{MoveInput.Describe(inputDir)}，0.5s / {DashDistance * DashSpeedMultiplier:0.00} 格，免疫伤害）");
            }

            // 挥剑
            if (attackRequested && CanAttack)
            {
                attackTimer = AttackDuration;
                attackCooldown = AttackDuration + AttackCooldown;
                sweepPrevRel = -90f;
                sweepRel = -90f;
                swingCount++;
                Note($"挥剑（面向 {MoveInput.Describe(facing)}，扇形 150°、半径 1 格）");
            }

            // 跳跃（文档 L31~L33）：空格发出跳跃指令 -> 进入浮空；0.75s 升 1.5 格、再 0.75s 落回。
            // 跳跃与行走是主角的两个分运动，空中仍按移动键水平移动（合运动）。
            if (jumpRequested && !Jumping)
            {
                jumpTimer = 0f;
                jumpCount++;
                Note($"跳跃（浮空 {JumpHeight:0.0} 格，{JumpTotalSeconds:0.00}s 落地）");
            }
            if (Jumping)
            {
                jumpTimer += dt;
                if (jumpTimer >= JumpTotalSeconds)
                {
                    jumpTimer = -1f;
                    Note("落回地面");
                }
            }

            // 位移：冲刺期间按冲刺速度；普通移动只认"这一帧按住的键"（反馈第 2 条：松开按键立刻停）
            Vector2 delta = Vector2.zero;
            float want = 0f;
            if (Dashing)
            {
                dashTimer = Mathf.Max(0f, dashTimer - dt);
                delta = dashDir * (DashSpeed * dt);
                want = delta.magnitude;
            }
            else if (!MoveInput.IsNone(newInputDir))
            {
                delta = MoveInput.ToVector(newInputDir) * (MoveSpeed * dt);
            }
            Move(map, delta, true);

            // 自我审查第 1 条：冲刺撞墙就立刻结束，不要贴着墙空转 0.5 秒（冷却照旧）
            if (Dashing && want > 1e-5f && moved.magnitude < want * 0.35f)
            {
                dashTimer = 0f;
                dashBlockedCount++;
                Note("冲刺撞墙，提前结束（冷却保留）");
            }

            // 剑刃这一帧扫过的角度区间（相对面向，逆时针递增）
            if (Attacking)
            {
                attackTimer = Mathf.Max(0f, attackTimer - dt);
                sweepPrevRel = sweepRel;
                sweepRel = BladeRelativeDeg;
            }
        }

        /// <summary>本帧剑刃是否扫过了相对角度区间 [a, b]。命中判定用（保证每只怪一次挥剑只挨一下）。</summary>
        public bool Swept(float a, float b)
        {
            if (!Attacking) return false;
            return a <= sweepRel && b >= sweepPrevRel;
        }

        /// <summary>受伤；冲刺过程中免疫伤害。返回是否真的掉血。</summary>
        public bool Hurt(int damage, string source)
        {
            if (Dashing) { Note($"{source}的攻击被冲刺免疫（冲刺中无敌）"); return false; }
            if (hurtInvul > 0f) { Note($"{source}的攻击被受伤无敌帧挡下（剩 {hurtInvul:0.0}s）"); return false; }
            hp = Mathf.Max(0, hp - damage);
            hurtFlash = HurtFlashSeconds;
            hurtInvul = HurtInvulSeconds;
            hurtCount++;
            Note($"被{source}击中：-{damage} HP（{hp}/{hpMax}）");
            return true;
        }

        public void RestoreFull()
        {
            hp = hpMax;
            inputDir = MoveInput.None;
            dashTimer = 0f; dashCooldown = 0f; attackTimer = 0f; attackCooldown = 0f; hurtFlash = 0f; hurtInvul = 0f; dashFade = 0f; jumpTimer = -1f;
        }

        public void Note(string s)
        {
            events.Add(s);
            if (events.Count > 30) events.RemoveAt(0);
        }

        public string LastEvent { get { return events.Count == 0 ? "" : events[events.Count - 1]; } }
    }
}