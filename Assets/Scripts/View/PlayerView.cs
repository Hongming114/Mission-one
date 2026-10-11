using UnityEngine;
using DungeonCity.Sim;

namespace DungeonCity.View
{
    /// <summary>
    /// 主角视图：读 PlayerSim 的状态做动画（不反向改逻辑）。
    ///   * 8 方向贴图 + 行走两帧（腿部动作）；
    ///   * 冲刺：每 0.045s 留一个残影（淡蓝色半透明，0.28s 淡出）；
    ///   * 挥剑：剑绕握剑手逆时针扫 180°，剑身带 3 个残影，同时显示 150° 刀光扇形（= 伤害范围）；
    ///   * 受击：贴图闪红（0.35s）。
    /// 贴图都是"面向相机的广告牌"（Billboard，只跟相机朝向），地面上铺一张影子。
    /// 屏幕平面角 = 面向角 + 相对角度：相机平面里逆时针为正，与逻辑层的角度约定一致。
    /// </summary>
    public class PlayerView : MonoBehaviour
    {
        public PlayerSim sim;
        public SpriteLibrary lib;
        public Camera cam;

        Transform billboard, swordPivot;
        SpriteRenderer shadow, body, slash, sword;
        readonly SpriteRenderer[] dashGhosts = new SpriteRenderer[6];
        readonly SpriteRenderer[] bladeGhosts = new SpriteRenderer[3];
        readonly float[] bladeGhostOffsets = { -18f, -42f, -68f };
        float animTimer, deadTimer, facingAngle;
        float ghostFade;        // 调试3-5: 残影整体透明度（冲刺结束后渐隐）

        // 调试3-5: 冲刺残影改为始终跟在角色身后的多重半透明分身（原冲击波/速度线已删除）

        public static PlayerView Create(string name, PlayerSim sim, SpriteLibrary lib, Camera cam, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var v = go.AddComponent<PlayerView>();
            v.sim = sim;
            v.lib = lib;
            v.cam = cam;
            v.Build();
            v.ApplyVisualImmediate();
            return v;
        }

        void Build()
        {
            shadow = NewRenderer("影子", transform, lib.Shadow(), 5, new Vector3(0f, 0.02f, 0f));
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var bb = new GameObject("广告牌");
            bb.transform.SetParent(transform, false);
            Billboard.Attach(bb, cam);
            billboard = bb.transform;

            body = NewRenderer("身体", billboard, lib.Player(0, 0), 10, Vector3.zero);

            var sp = new GameObject("握剑手");
            sp.transform.SetParent(billboard, false);
            sp.transform.localPosition = new Vector3(0.10f, 0.62f, 0f);
            swordPivot = sp.transform;
            sword = NewRenderer("剑", swordPivot, lib.Sword(), 11, Vector3.zero);

            for (int i = 0; i < bladeGhosts.Length; i++)
                bladeGhosts[i] = NewRenderer("剑残影" + i, billboard, lib.SwordGhost(), 9, new Vector3(0.10f, 0.62f, 0f));

            slash = NewRenderer("刀光", billboard, lib.Sector(), 8, new Vector3(0f, 0.5f, 0f));
            slash.enabled = false;

            for (int i = 0; i < dashGhosts.Length; i++)
            {
                dashGhosts[i] = NewRenderer("冲刺残影" + i, transform, lib.PlayerGhost(0, 1), 6, Vector3.zero);
                dashGhosts[i].enabled = false;
            }
        }

        SpriteRenderer NewRenderer(string name, Transform parent, Sprite sprite, int order, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.material = lib.SpriteMaterial;
            r.sortingOrder = order;
            return r;
        }

        public void ApplyVisualImmediate()
        {
            transform.position = Billboard.ToWorld(sim.pos);
            billboard.localPosition = new Vector3(0f, sim.AirHeight, 0f);   // 文档 L32：浮空（阴影留在原地，腿与阴影分开）
            if (billboard != null && cam != null) billboard.rotation = cam.transform.rotation;
            UpdateBodySprite(true);
            UpdateSword();
        }

        void Update()
        {
            transform.position = Billboard.ToWorld(sim.pos);

            billboard.localPosition = new Vector3(0f, sim.AirHeight, 0f);   // 文档 L31/L32：空中抬升，阴影留在地面
            if (shadow != null) shadow.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.72f, Mathf.Clamp01(sim.AirHeight / PlayerSim.JumpHeight));
            if (shadow != null)
            {
                // 调试3-3：跳跃时影子要变成深灰色（影子贴图本身很浅，用 tint 的 alpha 加深）
                float jumpK = Mathf.Clamp01(sim.AirHeight / PlayerSim.JumpHeight);
                shadow.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 2.6f, jumpK));
            }

            if (sim.Dead)
            {
                deadTimer += Time.deltaTime;
                float a = Mathf.Clamp01(1f - deadTimer / 0.6f);
                SetAlpha(body, a);
                SetAlpha(sword, a);
                SetAlpha(slash, a * 0.5f);
                if (deadTimer > 0.7f) gameObject.SetActive(false);
                return;
            }
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            UpdateBodySprite(false);
            UpdateSword();
            UpdateDashGhosts();
            UpdateBladeGhosts();
            UpdateSlash();
        }

        void UpdateBodySprite(bool immediate)
        {
            int dirIndex = MoveInput.SpriteIndex(sim.facing);
            bool walking = sim.moving || sim.Dashing;
            animTimer = walking ? animTimer + (immediate ? 0.05f : Time.deltaTime * 7f) : 0f;
            int frame = walking ? 1 + (Mathf.FloorToInt(animTimer) % 2) : 0;
            if (sim.Jumping) body.sprite = sim.hurtFlash > 0f ? lib.PlayerHurtFrame(dirIndex, frame) : lib.PlayerJump(dirIndex);   // 文档 L32：跳跃中切换为跳跃姿势
            else body.sprite = sim.hurtFlash > 0f ? lib.PlayerHurtFrame(dirIndex, frame) : lib.Player(dirIndex, frame);
            body.color = Color.white;
        }

        void UpdateSword()
        {
            facingAngle = Billboard.PlaneAngleDeg(cam, new Vector2(sim.facing.x, sim.facing.y));
            float bladeRel = sim.Attacking ? sim.BladeRelativeDeg : -55f;   // 待机：剑扛在肩上
            swordPivot.localRotation = Quaternion.Euler(0f, 0f, facingAngle + bladeRel);
            sword.enabled = true;
        }

        /// <summary>调试3-5：冲刺残影 = 冲刺时始终跟在角色身后的多重半透明分身，冲刺结束后一起渐隐消失。</summary>
        void UpdateDashGhosts()
        {
            // 冲刺中整体不透明度 1；冲刺结束后 0.18 秒内渐隐到 0（隐退效果地消失）
            if (sim.Dashing) ghostFade = 1f;
            else ghostFade = Mathf.Max(0f, ghostFade - Time.deltaTime / 0.18f);

            if (ghostFade <= 0f)
            {
                for (int i = 0; i < dashGhosts.Length; i++)
                {
                    if (dashGhosts[i] != null) dashGhosts[i].enabled = false;
                }
                return;
            }

            Vector2 dirV = sim.dashDir;                        // 冲刺方向（没有输入时 = 朝向）
            if (dirV.sqrMagnitude < 1e-4f) dirV = new Vector2(sim.facing.x, sim.facing.y);
            if (dirV.sqrMagnitude < 1e-4f) dirV = new Vector2(0f, -1f);
            dirV.Normalize();

            for (int i = 0; i < dashGhosts.Length; i++)
            {
                SpriteRenderer g = dashGhosts[i];
                if (g == null) continue;
                float off = 0.20f + 0.17f * i;                                  // 都在角色身后，位置各不相同
                float side = ((i % 2 == 0) ? 1f : -1f) * (0.03f + 0.02f * i);   // 轻微左右错开，能看出是多个
                Vector2 ground = sim.pos - dirV * off + new Vector2(-dirV.y, dirV.x) * side;
                Vector3 ghostPos = Billboard.ToWorld(ground, 0.02f);
                // 调试4：抬升改到相机平面内（沿 cam.up），否则抬高会让深度变小、残影可能穿到墙前面
                if (cam != null) ghostPos += cam.transform.up * sim.AirHeight;
                g.transform.position = ghostPos;
                g.transform.rotation = cam != null ? cam.transform.rotation : Quaternion.identity;
                g.sprite = lib.PlayerGhost(MoveInput.SpriteIndex(sim.facing), 1);
                g.enabled = true;
                SetAlpha(g, (0.42f - 0.05f * i) * ghostFade);                   // 越靠后越淡
            }
        }

        void UpdateBladeGhosts()
        {
            for (int i = 0; i < bladeGhosts.Length; i++)
            {
                if (!sim.Attacking) { bladeGhosts[i].enabled = false; continue; }
                float rel = Mathf.Max(-90f, sim.BladeRelativeDeg + bladeGhostOffsets[i]);
                bladeGhosts[i].enabled = true;
                bladeGhosts[i].transform.localRotation = Quaternion.Euler(0f, 0f, facingAngle + rel);
                SetAlpha(bladeGhosts[i], 0.34f - i * 0.10f);
            }
        }

        void UpdateSlash()
        {
            if (!sim.Attacking) { slash.enabled = false; return; }
            slash.enabled = true;
            slash.transform.localRotation = Quaternion.Euler(0f, 0f, facingAngle);
            float p = sim.AttackProgress;
            SetAlpha(slash, 0.18f + 0.42f * Mathf.Sin(p * Mathf.PI));
            slash.transform.localScale = Vector3.one * (0.92f + 0.08f * p);
        }

        static void SetAlpha(SpriteRenderer r, float a)
        {
            if (r == null) return;
            Color c = r.color;
            c.a = a;
            r.color = c;
        }
    }
}
