using System.Collections.Generic;
using UnityEngine;

namespace Breakout
{
    /// <summary>
    /// HUD 用的是最老的 IMGUI（OnGUI）。
    ///
    /// 为什么不用 uGUI / TextMeshPro：那两个都得先在编辑器里搭 Canvas、做字体资产，
    /// 而 OnGUI 在代码里写几个 Label 就能出字，最适合"一个脚本直接跑"的练手项目。
    /// 中文靠 Font.CreateDynamicFontFromOSFont 从系统字体里动态取，不用往工程里导入字体文件。
    /// </summary>
    public class BreakoutHud : MonoBehaviour
    {
        public GameManager game;

        Font _font;
        GUIStyle _hudL, _hudC, _hudR, _bigC, _smallC, _hint;
        readonly List<Popup> _pops = new List<Popup>();

        struct Popup { public Vector3 world; public string text; public float born; }

        void Awake()
        {
            _font = PickChineseFont();
        }

        static Font PickChineseFont()
        {
            string[] wanted =
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑",
                "SimHei", "黑体", "DengXian", "等线", "SimSun", "宋体", "Arial Unicode MS",
            };
            HashSet<string> installed = new HashSet<string>(Font.GetOSInstalledFontNames());
            foreach (string w in wanted)
            {
                if (!installed.Contains(w)) continue;
                Font f = Font.CreateDynamicFontFromOSFont(w, 24);
                if (f != null)
                {
                    Debug.Log("[打砖块] HUD 使用系统字体：" + w);
                    return f;
                }
            }
            Debug.LogWarning("[打砖块] 没找到中文字体，HUD 的中文可能显示成方块");
            return Font.CreateDynamicFontFromOSFont("Arial", 24);
        }

        public void Pop(Vector3 world, string text)
        {
            _pops.Add(new Popup { world = world, text = text, born = Time.time });
        }

        void MakeStyles()
        {
            _hudL = new GUIStyle(GUI.skin.label);
            _hudL.font = _font;
            _hudL.fontSize = 22;
            _hudL.fontStyle = FontStyle.Bold;
            _hudL.normal.textColor = Color.white;
            _hudL.alignment = TextAnchor.MiddleLeft;

            _hudC = new GUIStyle(_hudL); _hudC.alignment = TextAnchor.MiddleCenter;
            _hudR = new GUIStyle(_hudL); _hudR.alignment = TextAnchor.MiddleRight;

            _bigC = new GUIStyle(_hudC);
            _bigC.fontSize = 40;

            _smallC = new GUIStyle(_hudC);
            _smallC.fontSize = 17;
            _smallC.fontStyle = FontStyle.Normal;

            _hint = new GUIStyle(_smallC);
            _hint.normal.textColor = new Color(1f, 1f, 1f, 0.42f);
        }

        static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void OnGUI()
        {
            if (game == null || _font == null) return;
            if (_hudL == null || _hudL.font != _font) MakeStyles();

            int W = Screen.width;
            int H = Screen.height;

            // ---------------- 顶部信息条 ----------------
            Fill(new Rect(0f, 0f, W, 54f), new Color(0f, 0f, 0f, 0.42f));
            GUI.Label(new Rect(24f, 12f, 360f, 32f), "得分  " + game.score.ToString("N0"), _hudL);
            GUI.Label(new Rect(0f, 12f, W, 32f), "砖块  " + game.bricksLeft, _hudC);

            string hearts = "";
            for (int i = 0; i < game.startingLives; i++) hearts += (i < game.lives) ? "● " : "○ ";
            GUI.Label(new Rect(W - 384f, 12f, 360f, 32f), "生命  " + hearts, _hudR);

            // ---------------- 底部操作提示 ----------------
            GUI.Label(new Rect(0f, H - 36f, W, 26f),
                      "A / D 或 ← →  移动挡板          空格  发球          P  暂停          R  重开",
                      _hint);

            // ---------------- 居中状态提示 ----------------
            switch (game.state)
            {
                case GameState.Ready:
                    Fill(new Rect(0f, H * 0.60f, W, 74f), new Color(0f, 0f, 0f, 0.35f));
                    GUI.Label(new Rect(0f, H * 0.60f, W, 74f), "按 空格 发球", _bigC);
                    break;

                case GameState.Paused:
                    Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.55f));
                    GUI.Label(new Rect(0f, H * 0.40f, W, 60f), "已暂停", _bigC);
                    GUI.Label(new Rect(0f, H * 0.40f + 62f, W, 32f), "按 P 继续", _smallC);
                    break;

                case GameState.Won:
                    Fill(new Rect(0f, 0f, W, H), new Color(0.05f, 0.25f, 0.10f, 0.55f));
                    GUI.Label(new Rect(0f, H * 0.36f, W, 60f), "通关！全部砖块清空", _bigC);
                    GUI.Label(new Rect(0f, H * 0.36f + 64f, W, 34f), "最终得分 " + game.score.ToString("N0"), _smallC);
                    GUI.Label(new Rect(0f, H * 0.36f + 104f, W, 34f), "按 R 再来一局", _smallC);
                    break;

                case GameState.Lost:
                    Fill(new Rect(0f, 0f, W, H), new Color(0.30f, 0.05f, 0.08f, 0.55f));
                    GUI.Label(new Rect(0f, H * 0.36f, W, 60f), "游戏结束", _bigC);
                    GUI.Label(new Rect(0f, H * 0.36f + 64f, W, 34f), "最终得分 " + game.score.ToString("N0"), _smallC);
                    GUI.Label(new Rect(0f, H * 0.36f + 104f, W, 34f), "按 R 再来一局", _smallC);
                    break;
            }

            // ---------------- 加分飘字 ----------------
            if (_pops.Count > 0)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    for (int i = _pops.Count - 1; i >= 0; i--)
                    {
                        Popup p = _pops[i];
                        float age = Time.time - p.born;
                        if (age > 0.9f) { _pops.RemoveAt(i); continue; }

                        Vector3 sp = cam.WorldToScreenPoint(p.world);
                        if (sp.z < 0f) continue;                 // 在相机后面就别画了
                        float a = 1f - age / 0.9f;
                        Color old = GUI.color;
                        GUI.color = new Color(1f, 1f, 1f, a);
                        Rect r = new Rect(sp.x - 60f, Screen.height - sp.y - age * 60f - 14f, 120f, 28f);
                        GUI.Label(r, p.text, _smallC);
                        GUI.color = old;
                    }
                }
            }
        }
    }
}