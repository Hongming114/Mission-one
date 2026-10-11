using UnityEngine;
using UnityEngine.UI;
using DungeonCity.Game;

namespace DungeonCity.UI
{
    /// <summary>主界面：标题 + "开始游戏"按钮（点击后才生成世界）。</summary>
    public class MainMenuUI : MonoBehaviour
    {
        GameBootstrap boot;

        public static MainMenuUI Create(GameBootstrap boot)
        {
            var go = new GameObject("主界面");
            var ui = go.AddComponent<MainMenuUI>();
            ui.boot = boot;
            ui.Build();
            return ui;
        }

        void Build()
        {
            Canvas canvas = UiFactory.CreateCanvas("主界面画布", transform, 100);
            Transform root = canvas.transform;

            Image bg = UiFactory.NewImage("背景", root, new Color(0.055f, 0.065f, 0.095f, 1f));
            UiFactory.Stretch((RectTransform)bg.transform);

            Text title = UiFactory.NewText("标题", root, "地牢与城市", 96, TextAnchor.MiddleCenter, new Color(0.94f, 0.95f, 1f));
            UiFactory.Place((RectTransform)title.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 210f), new Vector2(1200f, 130f));

            Text sub = UiFactory.NewText("副标题", root, "DungeonCity · 侧俯视 2.5D 房间制地图 · 第一次世界生成", 30, TextAnchor.MiddleCenter, new Color(0.62f, 0.68f, 0.82f));
            UiFactory.Place((RectTransform)sub.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(1200f, 44f));

            Button start = UiFactory.NewButton("开始游戏按钮", root, "开始游戏", 46, OnStart);
            UiFactory.Place((RectTransform)start.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(420f, 96f));

            Text hint = UiFactory.NewText("操作提示", root,
                "目标：出生在城市房间的非不可达格（III ≠ 0）\n" +
                "W A S D / 方向键：按格子移动　|　走过 III = 2.1 的门格自动换房间\n" +
                "踩到 III = 2.2 的梯子格切换世界（城市 M=1 ↔ 地牢 M=2）\n" +
                "V：调试直接换世界　R：回到城市出生点　F1：开关 HUD",
                26, TextAnchor.UpperCenter, new Color(0.72f, 0.76f, 0.86f));
            UiFactory.Place((RectTransform)hint.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(1400f, 160f));
        }

        void OnStart()
        {
            if (boot != null) boot.StartGame();
            gameObject.SetActive(false);
        }
    }
}