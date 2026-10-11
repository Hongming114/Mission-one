// ============================================================================
//  UnityEngine.UI / EventSystems / Events 桩（编译体检用）
// ============================================================================
using System;
using UnityEngine.EventSystems;

namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public delegate void UnityAction<T0>(T0 a0);
    public delegate void UnityAction<T0, T1>(T0 a0, T1 a1);
    public delegate void UnityAction<T0, T1, T2>(T0 a0, T1 a1, T2 a2);
    // 注意：Unity 的 UnityAction 同时兼容带参/不带参的写法（这里用可变版本近似）
    public class UnityEventBase { public void RemoveAllListeners() { } public int GetPersistentEventCount() { return 0; } }
    public class UnityEvent : UnityEventBase
    {
        public void AddListener(UnityAction call) { }
        public void RemoveListener(UnityAction call) { }
        public void Invoke() { }
    }
    public class UnityEvent<T0> : UnityEventBase
    {
        public void AddListener(UnityAction<T0> call) { }
        public void RemoveListener(UnityAction<T0> call) { }
        public void Invoke(T0 a0) { }
    }
}

namespace UnityEngine.EventSystems
{
    public class UIBehaviour : MonoBehaviour { }
    public class EventSystem : UIBehaviour
    {
        public static EventSystem current { get { return null; } }
        public GameObject currentSelectedGameObject { get { return null; } }
        public void SetSelectedGameObject(GameObject go) { }
        public bool alreadySelecting { get { return false; } }
    }
    public class BaseInputModule : UIBehaviour { }
    public class PointerInputModule : BaseInputModule { }
    public class StandaloneInputModule : PointerInputModule { }
    public class BaseEventData { }
    public class PointerEventData : BaseEventData { public Vector2 position { get; set; } }
    public interface IEventSystemHandler { }
    public interface IPointerClickHandler : IEventSystemHandler { void OnPointerClick(PointerEventData e); }
}

namespace UnityEngine.UI
{
    public class Graphic : UIBehaviour
    {
        public Color color { get; set; }
        public bool raycastTarget { get; set; }
        public RectTransform rectTransform { get { return null; } }
        public Material material { get; set; }
        public void SetAllDirty() { }
        public void SetVerticesDirty() { }
        public Canvas canvas { get { return null; } }
        public int depth { get { return 0; } }
    }
    public class MaskableGraphic : Graphic { public bool maskable { get; set; } }
    public class Text : MaskableGraphic
    {
        public string text { get; set; }
        public Font font { get; set; }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextAnchor alignment { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public float lineSpacing { get; set; }
        public bool supportRichText { get; set; }
        public bool resizeTextForBestFit { get; set; }
        public int resizeTextMinSize { get; set; }
        public int resizeTextMaxSize { get; set; }
        public TextGenerator cachedTextGenerator { get { return null; } }
    }
    public class TextGenerator { }
    public class Image : MaskableGraphic
    {
        public Sprite sprite { get; set; }
        public Image.Type type { get; set; }
        public bool preserveAspect { get; set; }
        public bool fillCenter { get; set; }
        public float fillAmount { get; set; }
        public Image.FillMethod fillMethod { get; set; }
        public int fillOrigin { get; set; }
        public bool fillClockwise { get; set; }
        public float pixelsPerUnitMultiplier { get; set; }
        public float alphaHitTestMinimumThreshold { get; set; }
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
    }
    public class RawImage : MaskableGraphic { public Texture texture { get; set; } }
    public class Mask : UIBehaviour { public bool showMaskGraphic { get; set; } }
    public class RectMask2D : UIBehaviour { }
    public struct ColorBlock
    {
        public Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor;
        public float colorMultiplier, fadeDuration;
        public static ColorBlock defaultColorBlock { get { return new ColorBlock(); } }
    }
    public struct Navigation { public Mode mode; public enum Mode { None, Horizontal, Vertical, Automatic, Explicit } }
    public class Selectable : UIBehaviour
    {
        public bool interactable { get; set; }
        public Graphic targetGraphic { get; set; }
        public ColorBlock colors { get; set; }
        public Navigation navigation { get; set; }
        public Image image { get { return null; } }
    }
    public class Button : Selectable
    {
        [Serializable] public class ButtonClickedEvent : UnityEngine.Events.UnityEvent { }
        public ButtonClickedEvent onClick { get { return new ButtonClickedEvent(); } set { } }
        public void Select() { }
    }
    public class Toggle : Selectable { public bool isOn { get; set; } }
    public class Slider : Selectable { public float value { get; set; } public float minValue { get; set; } public float maxValue { get; set; } }
    public class CanvasScaler : UIBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
        public float referencePixelsPerUnit { get; set; }
        public float scaleFactor { get { return 1f; } }
    }
    public class GraphicRaycaster : UIBehaviour
    {
        public bool ignoreReversedGraphics { get; set; }
        public void Raycast(UnityEngine.EventSystems.PointerEventData e, System.Collections.Generic.List<RaycastResult> results) { }
    }
    public struct RaycastResult { public GameObject gameObject { get; set; } public int depth { get; set; } }
    public class LayoutElement : UIBehaviour { public float preferredWidth { get; set; } public float preferredHeight { get; set; } }
    public class LayoutGroup : UIBehaviour { }
    public class HorizontalOrVerticalLayoutGroup : LayoutGroup { public float spacing { get; set; } public bool childForceExpandWidth { get; set; } public bool childForceExpandHeight { get; set; } }
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class ContentSizeFitter : UIBehaviour { public enum FitMode { Unconstrained, MinSize, PreferredSize } public FitMode horizontalFit { get; set; } public FitMode verticalFit { get; set; } }
    public class Shadow : UIBehaviour { public Color effectColor { get; set; } public Vector2 effectDistance { get; set; } }
    public class Outline : Shadow { }
}

namespace UnityEngine
{
    public static class ImageConversion
    {
        public static byte[] EncodeToPNG(this Texture2D tex) { return new byte[0]; }
        public static byte[] EncodeToJPG(this Texture2D tex) { return new byte[0]; }
    }
    public static class JsonUtility
    {
        public static string ToJson(object o) { return "{}"; }
        public static T FromJson<T>(string s) { return default(T); }
    }
}