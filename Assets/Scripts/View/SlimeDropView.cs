using UnityEngine;
using DungeonCity.Sim;

namespace DungeonCity.View
{
    /// <summary>
    /// 掉落物视图（文档 L43）：只读 SlimeDrop 的位置，画一个约 0.6 格的"史莱姆清"（程序化占位图），
    /// 带一层淡绿微光与轻微上下浮动；被拾取后由 GameSession 销毁。
    /// </summary>
    public class SlimeDropView : MonoBehaviour
    {
        public SlimeDrop drop;
        public SpriteLibrary lib;
        public Camera cam;

        Transform billboard;
        SpriteRenderer body;
        SpriteRenderer glow;
        float t;

        public static SlimeDropView Create(string name, SlimeDrop drop, SpriteLibrary lib, Camera cam, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var v = go.AddComponent<SlimeDropView>();
            v.drop = drop;
            v.lib = lib;
            v.cam = cam;
            v.Build();
            return v;
        }

        void Build()
        {
            var bb = new GameObject("广告牌");
            bb.transform.SetParent(transform, false);
            Billboard.Attach(bb, cam);
            billboard = bb.transform;

            glow = NewRenderer("微光", billboard, lib.Blast(), 6, Vector3.zero);
            glow.color = new Color(0.62f, 1f, 0.74f, 0.30f);
            glow.transform.localScale = Vector3.one * 0.5f;

            body = NewRenderer("史莱姆清", billboard, lib.SlimeJelly(), 7, Vector3.zero);
            Apply(true);
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

        void Update()
        {
            if (drop == null || drop.picked) { gameObject.SetActive(false); return; }
            t += Time.deltaTime;
            Apply(false);
        }

        void Apply(bool immediate)
        {
            if (drop == null) return;
            float bob = immediate ? 0f : Mathf.Sin(t * 3.4f) * 0.05f;
            transform.position = Billboard.ToWorld(drop.pos, 0.06f + bob);
            if (billboard != null && cam != null) billboard.rotation = cam.transform.rotation;
            float pulse = immediate ? 1f : 1f + 0.06f * Mathf.Sin(t * 5f);
            body.transform.localScale = Vector3.one * pulse;
        }
    }
}
