using UnityEngine;
using UnityEngine.UI;
using DungeonCity.Game;
using DungeonCity.View;

namespace DungeonCity.UI
{
    /// <summary>
    /// 左上角血条：满血宽度 = 2 个方格在屏幕上的像素宽（按正交相机换算，2 * Screen.height / (2 * orthoSize)）。
    /// 方块的 pivot 靠左，所以血量减少时条是从右往左缩的。
    /// </summary>
    public class BloodBarUI : MonoBehaviour
    {
        public const float BarCells = 2f;      // 满血宽度（格）
        const float BarHeight = 28f;
        const float Pad = 2f;

        GameSession session;
        Camera cam;
        Image fill;
        Text label;
        RectTransform rootRect;
        float lastWidth = -1f;
        Text info;      // 自我审查第 8 条：正常模式也常驻的"世界 + 房间 + 最近怪距离"
        float blink;
        InventoryUI inventory;                                  // 调试3-4：点图标开关背包
        Image backpackIcon;                                     // 调试3-4：血条下方的背包图标
        SpriteLibrary lib;                                      // 图标用的精灵库

        public static BloodBarUI Create(GameSession session, Camera cam)
        {
            var go = new GameObject("血条");
            var ui = go.AddComponent<BloodBarUI>();
            ui.session = session;
            ui.cam = cam;
            ui.Build();
            return ui;
        }

        void Build()
        {
            Canvas canvas = UiFactory.CreateCanvas("血条画布", transform, 60);
            rootRect = UiFactory.NewRect("血条根", canvas.transform);
            UiFactory.Place(rootRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -14f), new Vector2(BarCells * 48f, BarHeight));

            Image back = UiFactory.NewImage("底", rootRect, new Color(0.05f, 0.05f, 0.07f, 0.88f));
            UiFactory.Stretch((RectTransform)back.transform);

            fill = UiFactory.NewImage("血量", rootRect, new Color(0.86f, 0.15f, 0.19f, 1f));
            RectTransform frt = (RectTransform)fill.transform;
            frt.anchorMin = new Vector2(0f, 0.5f);
            frt.anchorMax = new Vector2(0f, 0.5f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = new Vector2(Pad, 0f);
            frt.sizeDelta = new Vector2(BarCells * 48f - Pad * 2f, BarHeight - Pad * 2f);

            label = UiFactory.NewText("血条文字", rootRect, "", 15, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.96f));
            UiFactory.Stretch((RectTransform)label.transform);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            info = UiFactory.NewText("信息", canvas.transform, "", 16, TextAnchor.UpperLeft, new Color(0.92f, 0.95f, 1f, 0.95f));
            RectTransform irt = (RectTransform)info.transform;
            irt.anchorMin = new Vector2(0f, 1f); irt.anchorMax = new Vector2(0f, 1f); irt.pivot = new Vector2(0f, 1f);
            irt.anchoredPosition = new Vector2(18f, -14f - BarHeight - 42f);   // 调试3-4：给背包图标让位
            irt.sizeDelta = new Vector2(640f, 26f);
            info.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 调试3-4：血条下方的小背包图标（点击开关背包界面；坐标在 info 行之上）
            lib = new SpriteLibrary();
            Button backpack = UiFactory.NewButton("背包按钮", canvas.transform, "", 12, OnBackpackClick);
            UiFactory.Place((RectTransform)backpack.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -14f - BarHeight - 8f), new Vector2(32f, 32f));
            backpackIcon = UiFactory.NewImage("背包图标", backpack.transform, Color.white);
            backpackIcon.sprite = lib.BackpackIcon();
            backpackIcon.raycastTarget = false;
            UiFactory.Stretch((RectTransform)backpackIcon.transform, 3f, 3f, 3f, 3f);
        }

        /// <summary>调试3-4：把背包界面挂上，点图标即可开关。</summary>
        public void AttachInventory(InventoryUI inv) { inventory = inv; }

        void OnBackpackClick()
        {
            if (inventory != null) inventory.Toggle();
        }

        void LateUpdate()
        {
            if (session == null || fill == null) return;
            float ppu = cam != null ? Billboard.PixelsPerUnit(cam) : 32f;   // 1 格 = 多少屏幕像素
            float width = Mathf.Max(72f, BarCells * ppu);
            if (rootRect != null && Mathf.Abs(width - lastWidth) > 0.5f)
            {
                rootRect.sizeDelta = new Vector2(width, BarHeight);
                lastWidth = width;
            }
            float frac = session.player.hpMax <= 0 ? 0f : Mathf.Clamp01((float)session.player.hp / session.player.hpMax);
            RectTransform frt = (RectTransform)fill.transform;
            frt.sizeDelta = new Vector2(Mathf.Max(0f, width - Pad * 2f) * frac, BarHeight - Pad * 2f);
            if (label != null) label.text = $"{session.player.hp} / {session.player.hpMax}";

            if (info != null)
            {
                string room = session.InRoom ? $"({session.playerRoom.x},{session.playerRoom.y})" : "房间之间";
                float nearest = -1f;
                if (session.currentKind == DungeonCity.Core.WorldKind.Dungeon)
                {
                    for (int i = 0; i < session.slimes.Count; i++)
                    {
                        if (session.slimes[i].Dead) continue;
                        float d = Vector2.Distance(session.slimes[i].pos, session.player.pos);
                        if (nearest < 0f || d < nearest) nearest = d;
                    }
                }
                string near = nearest < 0f ? "—" : $"{nearest:0.0} 格";
                info.text = $"世界：{session.WorldNameCn}　房间：{room}　最近史莱姆：{near}{(session.debugMode ? "　[调试模式]" : "")}";
                info.color = nearest >= 0f && nearest < 6f ? new Color(1f, 0.45f, 0.4f) : new Color(0.92f, 0.95f, 1f, 0.95f);
            }

            // 自我审查第 7 条：血量低于 30% 时血条闪烁告警。
            float hpFrac = session.player.hpMax <= 0 ? 0f : (float)session.player.hp / session.player.hpMax;
            if (hpFrac < 0.3f)
            {
                blink += Time.deltaTime * 6f;
                fill.color = new Color(1f, 0.25f, 0.2f, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(blink)));
            }
            else fill.color = new Color(0.86f, 0.15f, 0.19f, 1f);
        }
    }
}