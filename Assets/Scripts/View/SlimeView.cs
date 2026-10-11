using UnityEngine;
using DungeonCity.Sim;

namespace DungeonCity.View
{
    /// <summary>
    /// 史莱姆视图：读 SlimeSim 的状态。
    ///   * 移动时 4 帧蠕动（约 8 帧/秒），静止时慢速呼吸（约 2 帧/秒）；
    ///   * 受击：贴图变红 + 被击退（位置由逻辑层算）；
    ///   * 蓄力：轻微抖动；扑击：压扁拉长（挤压感）；
    ///   * 死亡：缩小淡出后销毁。
    /// </summary>
    public class SlimeView : MonoBehaviour
    {
        public SlimeSim sim;
        public SpriteLibrary lib;
        public Camera cam;

        Transform billboard;
        SpriteRenderer shadow, body;
        float animTimer, deadTimer, wobble;
        float lastHp = -1f;

        public static SlimeView Create(string name, SlimeSim sim, SpriteLibrary lib, Camera cam, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var v = go.AddComponent<SlimeView>();
            v.sim = sim;
            v.lib = lib;
            v.cam = cam;
            v.Build();
            v.transform.position = Billboard.ToWorld(sim.pos);
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

            body = NewRenderer("身体", billboard, lib.Slime(0), 10, Vector3.zero);
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

        /// <summary>反馈 3：只显示与玩家同一个房间里的史莱姆。</summary>
        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        void Update()
        {
            transform.position = Billboard.ToWorld(sim.pos);

            if (sim.Dead)
            {
                deadTimer += Time.deltaTime;
                float k = Mathf.Clamp01(deadTimer / 0.45f);
                SetAlpha(body, 1f - k);
                SetAlpha(shadow, (1f - k) * 0.5f);
                transform.localScale = Vector3.one * (1f - 0.35f * k);
                if (deadTimer > 0.5f) Destroy(gameObject);
                return;
            }

            // 蠕动 / 呼吸
            bool moving = sim.moving;
            animTimer += Time.deltaTime * (moving ? 8f : 2f);
            int frame = Mathf.FloorToInt(animTimer) % 4;
            body.sprite = (sim.hurtFlash > 0f || sim.InHurt) ? lib.SlimeHurt() : lib.Slime(frame);

            // 蓄力抖动 / 扑击挤压
            wobble = Mathf.Sin(Time.time * 28f) * 0.05f;
            float sx = 1f, sy = 1f;
            if (sim.state == SlimeState.Charge)
            {
                float p = sim.ChargeProgress;
                sx = 1f + 0.10f * p + wobble;
                sy = 1f - 0.10f * p;
            }
            else if (sim.state == SlimeState.Lunge)
            {
                sx = 1.18f;
                sy = 0.86f;
            }
            else if (sim.state == SlimeState.Hurt)
            {
                sx = 1.10f;
                sy = 0.92f;
            }
            body.transform.localScale = new Vector3(sx, sy, 1f);

            // 受击红闪（贴图已经变红，这里再加一点亮度）
            body.color = sim.hurtFlash > 0f ? new Color(1f, 0.85f, 0.85f) : Color.white;
            lastHp = sim.hp;
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