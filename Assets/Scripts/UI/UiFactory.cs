using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonCity.UI
{
    /// <summary>代码建 UGUI 的小工具（不依赖预制体与场景资源）。</summary>
    public static class UiFactory
    {
        static Font _font;

        /// <summary>动态字体：优先用系统中文字体，保证中文能显示。</summary>
        public static Font Font()
        {
            if (_font != null) return _font;
            string[] candidates = { "Microsoft YaHei", "微软雅黑", "SimHei", "SimSun", "Microsoft JhengHei", "Arial" };
            try { _font = UnityEngine.Font.CreateDynamicFontFromOSFont(candidates, 36); }
            catch { _font = null; }
            if (_font == null)
            {
                try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            }
            if (_font == null) Debug.LogWarning("[UI] 找不到可用字体，中文可能显示为方块");
            return _font;
        }

        public static Canvas CreateCanvas(string name, Transform parent, int sortOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image NewImage(string name, Transform parent, Color color)
        {
            RectTransform rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text NewText(string name, Transform parent, string text, int size, TextAnchor anchor, Color color)
        {
            RectTransform rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font();
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button NewButton(string name, Transform parent, string label, int fontSize, UnityAction onClick)
        {
            RectTransform rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.22f, 0.26f, 0.36f, 0.96f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.35f, 1f);
            colors.pressedColor = new Color(0.75f, 0.78f, 0.9f, 1f);
            colors.selectedColor = Color.white;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);

            Text t = NewText("文字", rt, label, fontSize, TextAnchor.MiddleCenter, Color.white);
            Stretch((RectTransform)t.transform);
            return btn;
        }

        public static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            if (size.x > 0f && size.y > 0f) rt.sizeDelta = size;
        }

        public static RectTransform Panel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            Image img = NewImage(name, parent, color);
            Place((RectTransform)img.transform, anchorMin, anchorMax, pivot, anchoredPos, size);
            return (RectTransform)img.transform;
        }
    }
}