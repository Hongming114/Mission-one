using DungeonCity.Game;
using DungeonCity.Sim;
using DungeonCity.View;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonCity.UI
{
    /// <summary>
    /// 调试3-4：背包界面（5 x 10 = 50 格，正好对应 Inventory.SlotCount）。
    /// 按 B 键或点血条下方的背包图标开关；格子里画物品图标与数量，空格子只留暗色底。
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        public const int Columns = 10;
        public const int Rows = 5;
        public const float CellSize = 44f;
        public const float CellGap = 6f;
        public const float RefreshInterval = 0.2f;

        GameSession session;
        SpriteLibrary lib;
        RectTransform panel;
        RectTransform grid;
        Text title;
        readonly Image[] frames = new Image[Inventory.SlotCount];
        readonly Image[] icons = new Image[Inventory.SlotCount];
        readonly Text[] counts = new Text[Inventory.SlotCount];
        readonly string[] lastItem = new string[Inventory.SlotCount];
        readonly int[] lastAmount = new int[Inventory.SlotCount];
        float timer;

        public bool IsOpen { get { return panel != null && panel.gameObject.activeSelf; } }

        public static InventoryUI Create(GameSession session)
        {
            var go = new GameObject("背包界面");
            var ui = go.AddComponent<InventoryUI>();
            ui.session = session;
            ui.lib = new SpriteLibrary();
            ui.Build();
            return ui;
        }

        void Build()
        {
            Canvas canvas = UiFactory.CreateCanvas("背包画布", transform, 65);
            float gridW = Columns * CellSize + (Columns - 1) * CellGap;
            float gridH = Rows * CellSize + (Rows - 1) * CellGap;
            float w = gridW + 28f;
            float h = gridH + 64f;

            panel = UiFactory.Panel("背包面板", canvas.transform, new Color(0.06f, 0.07f, 0.10f, 0.94f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));

            title = UiFactory.NewText("标题", panel, "", 18, TextAnchor.UpperCenter, new Color(0.95f, 0.97f, 1f, 0.98f));
            UiFactory.Place((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), new Vector2(0f, 26f));
            title.horizontalOverflow = HorizontalWrapMode.Overflow;

            grid = UiFactory.NewRect("格子区", panel);
            UiFactory.Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -8f), new Vector2(gridW, gridH));

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    int index = r * Columns + c;
                    Image frame = UiFactory.NewImage("格" + (index + 1), grid, new Color(0.16f, 0.17f, 0.21f, 0.95f));
                    UiFactory.Place((RectTransform)frame.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(c * (CellSize + CellGap), -r * (CellSize + CellGap)), new Vector2(CellSize, CellSize));
                    frames[index] = frame;

                    Image icon = UiFactory.NewImage("图标" + (index + 1), frame.transform, Color.white);
                    UiFactory.Stretch((RectTransform)icon.transform, 6f, 6f, 6f, 13f);
                    icon.raycastTarget = false;
                    icon.enabled = false;
                    icons[index] = icon;

                    Text num = UiFactory.NewText("数量" + (index + 1), frame.transform, "", 13, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.95f));
                    UiFactory.Stretch((RectTransform)num.transform, 2f, 1f, 4f, 1f);
                    counts[index] = num;

                    Text slotNo = UiFactory.NewText("编号" + (index + 1), frame.transform, (index + 1).ToString(), 10, TextAnchor.UpperLeft, new Color(0.75f, 0.80f, 0.90f, 0.55f));
                    UiFactory.Stretch((RectTransform)slotNo.transform, 3f, 1f, 1f, 1f);
                }
            }

            Refresh();
            panel.gameObject.SetActive(false);
        }
        public void Toggle() { SetOpen(!IsOpen); }

        public void SetOpen(bool open)
        {
            if (panel == null) return;
            panel.gameObject.SetActive(open);
            if (open)
            {
                timer = 0f;
                Refresh();      // 打开时立刻刷一遍，不等 0.2 秒
            }
        }

        void Update()
        {
            if (!IsOpen) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = RefreshInterval;
            Refresh();
        }

        void Refresh()
        {
            Inventory inv = session != null ? session.inventory : null;
            if (inv == null) return;

            if (title != null)
            {
                title.text = "背包　" + inv.UsedSlots + " / " + Inventory.SlotCount + " 格　（同种物品占一格，单格上限 " + Inventory.StackMax + "；按 B 或点左上角背包图标开关）";
            }

            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                string item = inv.SlotItem(i + 1);
                int amount = inv.SlotAmount(i + 1);
                bool filled = !string.IsNullOrEmpty(item) && amount > 0;
                if (lastItem[i] == item && lastAmount[i] == amount) continue;   // 只在内容变化时动 UI
                lastItem[i] = item;
                lastAmount[i] = amount;

                if (frames[i] != null)
                {
                    frames[i].color = filled ? new Color(0.22f, 0.26f, 0.32f, 0.98f) : new Color(0.13f, 0.14f, 0.18f, 0.85f);
                }
                if (icons[i] != null)
                {
                    icons[i].enabled = filled;
                    if (filled)
                    {
                        bool jelly = item == SlimeDrop.ItemName;
                        icons[i].sprite = jelly ? lib.SlimeJelly() : lib.White();
                        icons[i].color = jelly ? Color.white : new Color(0.85f, 0.76f, 0.38f, 1f);
                    }
                }
                if (counts[i] != null) counts[i].text = filled ? amount.ToString() : "";
            }
        }
    }
}
