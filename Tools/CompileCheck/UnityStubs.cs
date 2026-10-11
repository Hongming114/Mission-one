// ============================================================================
//  UnityEngine 桩（只为"没有 Unity 也能编译一遍"用，不追求运行期行为正确）
//  只实现 Assets\Scripts 用到的那部分 API 表面。
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name = "";
        public HideFlags hideFlags;
        public static void Destroy(Object obj) { }
        public static void Destroy(Object obj, float t) { }
        public static void DestroyImmediate(Object obj) { }
        public static T FindFirstObjectByType<T>() where T : Object { return null; }
        public static T[] FindObjectsByType<T>() where T : Object { return new T[0]; }
        public override string ToString() { return name; }
    }

    [Flags] public enum HideFlags { None = 0, HideInHierarchy = 1, DontSave = 4, HideAndDontSave = 61 }

    public class Component : Object
    {
        public GameObject gameObject { get; set; }
        public Transform transform { get { return gameObject != null ? gameObject.transform : null; } }
        public string tag { get { return gameObject != null ? gameObject.tag : ""; } set { if (gameObject != null) gameObject.tag = value; } }
        public T GetComponent<T>() where T : Component { return null; }
        public T GetComponentInChildren<T>() where T : Component { return null; }
        public T GetComponentInParent<T>() where T : Component { return null; }
        public T[] GetComponentsInChildren<T>() where T : Component { return new T[0]; }
        public bool TryGetComponent<T>(out T c) where T : Component { c = null; return false; }
    }

    public class Behaviour : Component { public bool enabled { get; set; } public bool isActiveAndEnabled { get { return enabled; } } }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) { return null; }
        public void StopCoroutine(IEnumerator routine) { }
        public void StopAllCoroutines() { }
        public void Invoke(string method, float time) { }
        public void CancelInvoke() { }
    }

    public class Coroutine { }
    public class YieldInstruction { }
    public class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s) { } }

    public class GameObject : Object
    {
        public string tag = "Untagged";
        public int layer;
        public bool activeSelf = true;
        public bool isStatic;
        readonly List<Component> components = new List<Component>();
        public Transform transform { get; private set; }

        public GameObject() : this("GameObject") { }
        public GameObject(string name) { this.name = name; transform = new Transform(this); }
        public GameObject(string name, params Type[] comps) { this.name = name; transform = new Transform(this); }

        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T(); c.gameObject = this; components.Add(c); return c;
        }
        public Component AddComponent(Type t) { return null; }
        public T GetComponent<T>() where T : Component { return null; }
        public T GetComponentInChildren<T>() where T : Component { return null; }
        public T[] GetComponentsInChildren<T>() where T : Component { return new T[0]; }
        public void SetActive(bool value) { activeSelf = value; }
        public static GameObject Find(string n) { return null; }
    }

    public class Transform : Component, IEnumerable
    {
        readonly List<Transform> children = new List<Transform>();
        public Transform parent;
        public Vector3 position, localPosition, localScale = Vector3.one, lossyScale = Vector3.one;
        public Quaternion rotation = Quaternion.identity, localRotation = Quaternion.identity;
        public Vector3 eulerAngles, localEulerAngles;

        public Transform() { }
        public Transform(GameObject go) { gameObject = go; }

        public Vector3 forward { get { return Vector3.forward; } set { } }
        public Vector3 up { get { return Vector3.up; } set { } }
        public Vector3 right { get { return Vector3.right; } set { } }
        public int childCount { get { return children.Count; } }

        public void SetParent(Transform p) { SetParent(p, true); }
        public void SetParent(Transform p, bool worldPositionStays) { parent = p; if (p != null) p.children.Add(this); }
        public Transform GetChild(int i) { return children[i]; }
        public Transform Find(string n) { return null; }
        public void SetAsLastSibling() { }
        public void SetAsFirstSibling() { }
        public void SetSiblingIndex(int i) { }
        public void Translate(Vector3 d) { position += d; }
        public void Rotate(Vector3 e) { }
        public void LookAt(Transform t) { }
        public void LookAt(Vector3 p) { }
        public Vector3 TransformPoint(Vector3 p) { return p; }
        public Vector3 InverseTransformPoint(Vector3 p) { return p; }
        public Vector3 TransformDirection(Vector3 d) { return d; }
        public IEnumerator GetEnumerator() { return children.GetEnumerator(); }
    }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin, anchorMax, pivot = new Vector2(0.5f, 0.5f), anchoredPosition, sizeDelta;
        public Vector2 offsetMin, offsetMax;
        public Vector3 anchoredPosition3D;
        public Rect rect = new Rect(0, 0, 100, 100);
        public void SetInsetAndSizeFromParentEdge(int edge, float inset, float size) { }
        public void GetWorldCorners(Vector3[] corners) { }
    }

    // ---------------------------------------------------------------- 数学
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero { get { return new Vector2(0, 0); } }
        public static Vector2 one { get { return new Vector2(1, 1); } }
        public static Vector2 up { get { return new Vector2(0, 1); } }
        public static Vector2 down { get { return new Vector2(0, -1); } }
        public static Vector2 left { get { return new Vector2(-1, 0); } }
        public static Vector2 right { get { return new Vector2(1, 0); } }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y); } }
        public float sqrMagnitude { get { return x * x + y * y; } }
        public Vector2 normalized { get { float m = magnitude; return m > 1e-6f ? new Vector2(x / m, y / m) : zero; } }
        public void Normalize() { this = normalized; }
        public void Set(float nx, float ny) { x = nx; y = ny; }
        public static float Distance(Vector2 a, Vector2 b) { return (a - b).magnitude; }
        public static float Dot(Vector2 a, Vector2 b) { return a.x * b.x + a.y * b.y; }
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { return a + (b - a) * Mathf.Clamp01(t); }
        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) { return a + (b - a) * t; }
        public static Vector2 MoveTowards(Vector2 a, Vector2 b, float d) { return a; }
        public static Vector2 Scale(Vector2 a, Vector2 b) { return new Vector2(a.x * b.x, a.y * b.y); }
        public static Vector2 Min(Vector2 a, Vector2 b) { return new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y)); }
        public static Vector2 Max(Vector2 a, Vector2 b) { return new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)); }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator -(Vector2 a) { return new Vector2(-a.x, -a.y); }
        public static Vector2 operator *(Vector2 a, float f) { return new Vector2(a.x * f, a.y * f); }
        public static Vector2 operator *(float f, Vector2 a) { return a * f; }
        public static Vector2 operator /(Vector2 a, float f) { return new Vector2(a.x / f, a.y / f); }
        public static bool operator ==(Vector2 a, Vector2 b) { return (a - b).sqrMagnitude < 1e-10f; }
        public static bool operator !=(Vector2 a, Vector2 b) { return !(a == b); }
        public override bool Equals(object o) { return o is Vector2 && this == (Vector2)o; }
        public override int GetHashCode() { return x.GetHashCode() ^ (y.GetHashCode() << 2); }
        public override string ToString() { return "(" + x.ToString("0.###") + ", " + y.ToString("0.###") + ")"; }
        public static implicit operator Vector3(Vector2 v) { return new Vector3(v.x, v.y, 0f); }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0f; }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 down { get { return new Vector3(0, -1, 0); } }
        public static Vector3 left { get { return new Vector3(-1, 0, 0); } }
        public static Vector3 right { get { return new Vector3(1, 0, 0); } }
        public static Vector3 forward { get { return new Vector3(0, 0, 1); } }
        public static Vector3 back { get { return new Vector3(0, 0, -1); } }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y + z * z); } }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public Vector3 normalized { get { float m = magnitude; return m > 1e-6f ? new Vector3(x / m, y / m, z / m) : zero; } }
        public void Normalize() { this = normalized; }
        public void Set(float nx, float ny, float nz) { x = nx; y = ny; z = nz; }
        public static float Distance(Vector3 a, Vector3 b) { return (a - b).magnitude; }
        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public static Vector3 Cross(Vector3 a, Vector3 b) { return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x); }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { return a + (b - a) * Mathf.Clamp01(t); }
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) { return a + (b - a) * t; }
        public static Vector3 MoveTowards(Vector3 a, Vector3 b, float d) { return a; }
        public static Vector3 Scale(Vector3 a, Vector3 b) { return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z); }
        public static Vector3 Min(Vector3 a, Vector3 b) { return new Vector3(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z)); }
        public static Vector3 Max(Vector3 a, Vector3 b) { return new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z)); }
        public static Vector3 ClampMagnitude(Vector3 v, float m) { return v; }
        public static float Angle(Vector3 a, Vector3 b) { return 0f; }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        public static Vector3 operator *(Vector3 a, float f) { return new Vector3(a.x * f, a.y * f, a.z * f); }
        public static Vector3 operator *(float f, Vector3 a) { return a * f; }
        public static Vector3 operator /(Vector3 a, float f) { return new Vector3(a.x / f, a.y / f, a.z / f); }
        public static bool operator ==(Vector3 a, Vector3 b) { return (a - b).sqrMagnitude < 1e-10f; }
        public static bool operator !=(Vector3 a, Vector3 b) { return !(a == b); }
        public override bool Equals(object o) { return o is Vector3 && this == (Vector3)o; }
        public override int GetHashCode() { return x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2); }
        public override string ToString() { return "(" + x.ToString("0.###") + ", " + y.ToString("0.###") + ", " + z.ToString("0.###") + ")"; }
        public static implicit operator Vector2(Vector3 v) { return new Vector2(v.x, v.y); }
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Vector4 zero { get { return new Vector4(0, 0, 0, 0); } }
        public static Vector4 one { get { return new Vector4(1, 1, 1, 1); } }
        public static implicit operator Color(Vector4 v) { return new Color(v.x, v.y, v.z, v.w); }
    }

    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static Vector2Int zero { get { return new Vector2Int(0, 0); } }
        public static Vector2Int one { get { return new Vector2Int(1, 1); } }
        public static Vector2Int up { get { return new Vector2Int(0, 1); } }
        public static Vector2Int down { get { return new Vector2Int(0, -1); } }
        public static Vector2Int left { get { return new Vector2Int(-1, 0); } }
        public static Vector2Int right { get { return new Vector2Int(1, 0); } }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y); } }
        public int sqrMagnitude { get { return x * x + y * y; } }
        public void Set(int nx, int ny) { x = nx; y = ny; }
        public static Vector2Int FloorToInt(Vector2 v) { return new Vector2Int(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y)); }
        public static Vector2Int CeilToInt(Vector2 v) { return new Vector2Int(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y)); }
        public static Vector2Int RoundToInt(Vector2 v) { return new Vector2Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y)); }
        public static Vector2Int operator +(Vector2Int a, Vector2Int b) { return new Vector2Int(a.x + b.x, a.y + b.y); }
        public static Vector2Int operator -(Vector2Int a, Vector2Int b) { return new Vector2Int(a.x - b.x, a.y - b.y); }
        public static Vector2Int operator -(Vector2Int a) { return new Vector2Int(-a.x, -a.y); }
        public static Vector2Int operator *(Vector2Int a, int f) { return new Vector2Int(a.x * f, a.y * f); }
        public static bool operator ==(Vector2Int a, Vector2Int b) { return a.x == b.x && a.y == b.y; }
        public static bool operator !=(Vector2Int a, Vector2Int b) { return !(a == b); }
        public bool Equals(Vector2Int o) { return this == o; }
        public override bool Equals(object o) { return o is Vector2Int && this == (Vector2Int)o; }
        public override int GetHashCode() { return x * 397 ^ y; }
        public override string ToString() { return "(" + x + ", " + y + ")"; }
        public static implicit operator Vector2(Vector2Int v) { return new Vector2(v.x, v.y); }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity { get { return new Quaternion(0, 0, 0, 1); } }
        public Vector3 eulerAngles { get { return Vector3.zero; } set { } }
        public static Quaternion Euler(float x, float y, float z) { return identity; }
        public static Quaternion Euler(Vector3 v) { return identity; }
        public static Quaternion AngleAxis(float angle, Vector3 axis) { return identity; }
        public static Quaternion LookRotation(Vector3 forward) { return identity; }
        public static Quaternion LookRotation(Vector3 forward, Vector3 up) { return identity; }
        public static Quaternion FromToRotation(Vector3 a, Vector3 b) { return identity; }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) { return identity; }
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) { return identity; }
        public static Quaternion RotateTowards(Quaternion a, Quaternion b, float d) { return identity; }
        public static Quaternion Inverse(Quaternion q) { return identity; }
        public static float Angle(Quaternion a, Quaternion b) { return 0f; }
        public static Quaternion operator *(Quaternion a, Quaternion b) { return identity; }
        public static Vector3 operator *(Quaternion q, Vector3 v) { return v; }
        public static Vector3 operator *(Quaternion q, Vector2 v) { return (Vector3)v; }
        public static Vector3 operator *(Quaternion q, Vector3Int v) { return Vector3.zero; }
    }

    public struct Vector3Int
    {
        public int x, y, z;
        public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3Int zero { get { return new Vector3Int(0, 0, 0); } }
        public static implicit operator Vector3(Vector3Int v) { return new Vector3(v.x, v.y, v.z); }
    }

    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public const float Rad2Deg = (float)(180.0 / Math.PI);
        public const float Epsilon = float.Epsilon;
        public static float Abs(float f) { return Math.Abs(f); }
        public static int Abs(int f) { return Math.Abs(f); }
        public static float Sign(float f) { return f < 0f ? -1f : 1f; }
        public static float Min(float a, float b) { return Math.Min(a, b); }
        public static int Min(int a, int b) { return Math.Min(a, b); }
        public static float Max(float a, float b) { return Math.Max(a, b); }
        public static int Max(int a, int b) { return Math.Max(a, b); }
        public static float Max(params float[] v) { float m = float.NegativeInfinity; foreach (float f in v) if (f > m) m = f; return m; }
        public static int Max(params int[] v) { int m = int.MinValue; foreach (int f in v) if (f > m) m = f; return m; }
        public static float Min(params float[] v) { float m = float.PositiveInfinity; foreach (float f in v) if (f < m) m = f; return m; }
        public static int Min(params int[] v) { int m = int.MaxValue; foreach (int f in v) if (f < m) m = f; return m; }
        public static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
        public static int Clamp(int v, int a, int b) { return v < a ? a : (v > b ? b : v); }
        public static float Clamp01(float v) { return Clamp(v, 0f, 1f); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static float LerpUnclamped(float a, float b, float t) { return a + (b - a) * t; }
        public static float InverseLerp(float a, float b, float v) { return Math.Abs(b - a) < 1e-9f ? 0f : Clamp01((v - a) / (b - a)); }
        public static float Floor(float f) { return (float)Math.Floor(f); }
        public static float Ceil(float f) { return (float)Math.Ceiling(f); }
        public static float Round(float f) { return (float)Math.Round(f); }
        public static int FloorToInt(float f) { return (int)Math.Floor(f); }
        public static int CeilToInt(float f) { return (int)Math.Ceiling(f); }
        public static int RoundToInt(float f) { return (int)Math.Round(f, MidpointRounding.AwayFromZero); }
        public static float Sqrt(float f) { return (float)Math.Sqrt(f); }
        public static float Pow(float f, float p) { return (float)Math.Pow(f, p); }
        public static float Exp(float f) { return (float)Math.Exp(f); }
        public static float Log(float f) { return (float)Math.Log(f); }
        public static float Log10(float f) { return (float)Math.Log10(f); }
        public static float Sin(float f) { return (float)Math.Sin(f); }
        public static float Cos(float f) { return (float)Math.Cos(f); }
        public static float Tan(float f) { return (float)Math.Tan(f); }
        public static float Asin(float f) { return (float)Math.Asin(f); }
        public static float Acos(float f) { return (float)Math.Acos(f); }
        public static float Atan(float f) { return (float)Math.Atan(f); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Repeat(float t, float length) { return t - Floor(t / length) * length; }
        public static float PingPong(float t, float length) { return length - Abs(Repeat(t, length * 2f) - length); }
        public static float MoveTowards(float a, float b, float d) { return Abs(b - a) <= d ? b : a + Sign(b - a) * d; }
        public static float SmoothStep(float a, float b, float t) { t = Clamp01(t); return a + (b - a) * t * t * (3f - 2f * t); }
        public static float DeltaAngle(float a, float b) { float d = Repeat(b - a, 360f); if (d > 180f) d -= 360f; return d; }
        public static bool Approximately(float a, float b) { return Abs(b - a) < 1e-5f; }
        public static float PerlinNoise(float x, float y) { return 0f; }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white { get { return new Color(1, 1, 1, 1); } }
        public static Color black { get { return new Color(0, 0, 0, 1); } }
        public static Color clear { get { return new Color(0, 0, 0, 0); } }
        public static Color red { get { return new Color(1, 0, 0, 1); } }
        public static Color green { get { return new Color(0, 1, 0, 1); } }
        public static Color blue { get { return new Color(0, 0, 1, 1); } }
        public static Color yellow { get { return new Color(1, 0.92f, 0.016f, 1); } }
        public static Color cyan { get { return new Color(0, 1, 1, 1); } }
        public static Color magenta { get { return new Color(1, 0, 1, 1); } }
        public static Color gray { get { return new Color(0.5f, 0.5f, 0.5f, 1); } }
        public static Color grey { get { return gray; } }
        public static Color Lerp(Color x, Color y, float t) { t = Mathf.Clamp01(t); return new Color(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t); }
        public Color gamma { get { return this; } }
        public float grayscale { get { return 0.299f * r + 0.587f * g + 0.114f * b; } }
        public static Color operator *(Color c, float f) { return new Color(c.r * f, c.g * f, c.b * f, c.a * f); }
        public static Color operator *(float f, Color c) { return c * f; }
        public static Color operator *(Color a, Color b) { return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a); }
        public static Color operator +(Color a, Color b) { return new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a); }
        public static Color operator -(Color a, Color b) { return new Color(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a); }
        public static bool operator ==(Color a, Color b) { return Mathf.Abs(a.r - b.r) < 1e-5f && Mathf.Abs(a.g - b.g) < 1e-5f && Mathf.Abs(a.b - b.b) < 1e-5f && Mathf.Abs(a.a - b.a) < 1e-5f; }
        public static bool operator !=(Color a, Color b) { return !(a == b); }
        public override bool Equals(object o) { return o is Color && this == (Color)o; }
        public override int GetHashCode() { return r.GetHashCode() ^ (g.GetHashCode() << 2) ^ (b.GetHashCode() >> 2) ^ (a.GetHashCode() >> 1); }
        public override string ToString() { return "RGBA(" + r + ", " + g + ", " + b + ", " + a + ")"; }
        public static implicit operator Color32(Color c) { return new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(Mathf.Clamp01(c.a) * 255f)); }
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color32 Lerp(Color32 x, Color32 y, float t) { return (Color32)Color.Lerp((Color)x, (Color)y, t); }
        public static implicit operator Color(Color32 c) { return new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f); }
        public override string ToString() { return "RGBA(" + r + ", " + g + ", " + b + ", " + a + ")"; }
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public Vector2 position { get { return new Vector2(x, y); } set { x = value.x; y = value.y; } }
        public Vector2 size { get { return new Vector2(width, height); } set { width = value.x; height = value.y; } }
        public Vector2 center { get { return new Vector2(x + width * 0.5f, y + height * 0.5f); } }
        public Vector2 min { get { return new Vector2(x, y); } }
        public Vector2 max { get { return new Vector2(x + width, y + height); } }
        public float xMin { get { return x; } } public float yMin { get { return y; } }
        public float xMax { get { return x + width; } } public float yMax { get { return y + height; } }
        public bool Contains(Vector2 p) { return p.x >= x && p.x <= x + width && p.y >= y && p.y <= y + height; }
    }

    public struct Bounds
    {
        public Vector3 center, size;
        public Bounds(Vector3 center, Vector3 size) { this.center = center; this.size = size; }
        public Vector3 extents { get { return size * 0.5f; } set { size = value * 2f; } }
        public Vector3 min { get { return center - extents; } set { SetMinMax(value, max); } }
        public Vector3 max { get { return center + extents; } set { SetMinMax(min, value); } }
        public void SetMinMax(Vector3 mn, Vector3 mx) { extents = (mx - mn) * 0.5f; center = mn + extents; }
        public void Encapsulate(Bounds b) { }
        public void Encapsulate(Vector3 p) { }
        public void Expand(float amount) { }
        public bool Contains(Vector3 p) { return false; }
        public bool Intersects(Bounds b) { return false; }
    }

    public static class Screen { public static int width { get { return 1920; } } public static int height { get { return 1080; } } public static float dpi { get { return 96f; } } }
    public static class Time
    {
        public static float deltaTime { get { return 0.016f; } }
        public static float unscaledDeltaTime { get { return 0.016f; } }
        public static float fixedDeltaTime { get { return 0.02f; } }
        public static float time { get { return 0f; } }
        public static float timeScale { get; set; }
        public static int frameCount { get { return 0; } }
    }
    public enum LogType { Error, Assert, Warning, Log, Exception }

    public static class Application
    {
        public static string dataPath { get { return "."; } }
        public static bool isBatchMode { get { return false; } }
        public static string unityVersion { get { return "6000.5.2f1"; } }
        public delegate void LogCallback(string condition, string stackTrace, LogType type);
        public static event LogCallback logMessageReceived;
        public static void RaiseLog(string condition, string stackTrace, LogType type) { if (logMessageReceived != null) logMessageReceived(condition, stackTrace, type); }
        public static bool isPlaying { get { return false; } }
        public static bool isEditor { get { return true; } }
        public static void Quit() { }
    }
    public static class Resources
    {
        public static T GetBuiltinResource<T>(string path) where T : Object { return null; }
        public static T Load<T>(string path) where T : Object { return null; }
        public static Object Load(string path) { return null; }
    }
    public static class Debug
    {
        public static void Log(object msg) { Application.RaiseLog(msg == null ? "null" : msg.ToString(), "", LogType.Log); }
        public static void LogWarning(object msg) { Application.RaiseLog(msg == null ? "null" : msg.ToString(), "", LogType.Warning); }
        public static void LogError(object msg) { Application.RaiseLog(msg == null ? "null" : msg.ToString(), "", LogType.Error); }
        public static void LogException(Exception e) { Application.RaiseLog(e == null ? "null" : e.ToString(), "", LogType.Exception); }
        public static void Assert(bool cond, string msg) { }
    }

    public enum KeyCode { None, W, A, S, D, R, V, Q, E, F1, F2, Space, Escape, LeftShift, RightShift, UpArrow, DownArrow, LeftArrow, RightArrow, Tab, LeftControl, RightControl, LeftAlt, RightAlt, T, P, B }
    public static class Input
    {
        public static bool GetKey(KeyCode k) { return false; }
        public static bool GetKeyDown(KeyCode k) { return false; }
        public static bool GetKeyUp(KeyCode k) { return false; }
        public static bool GetMouseButton(int b) { return false; }
        public static bool GetMouseButtonDown(int b) { return false; }
        public static bool GetMouseButtonUp(int b) { return false; }
        public static Vector3 mousePosition { get { return Vector3.zero; } }
        public static Vector2 mouseScrollDelta { get { return Vector2.zero; } }
        public static float GetAxis(string name) { return 0f; }
        public static float GetAxisRaw(string name) { return 0f; }
    }

    // ------------------------------------------------------------- 渲染相关
    public class Shader : Object
    {
        public static Shader Find(string name) { return new Shader { name = name }; }
        public bool HasProperty(string n) { return true; }
        public bool isSupported { get { return true; } }
    }
    public class Texture : Object
    {
        public int width { get; set; }
        public int height { get; set; }
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }
        public int anisoLevel { get; set; }
        public int mipmapCount { get { return 1; } }
    }
    public class Texture2D : Texture
    {
        public Texture2D(int w, int h) { width = w; height = h; }
        public Texture2D(int w, int h, TextureFormat fmt, bool mipChain) { width = w; height = h; }
        public void SetPixel(int x, int y, Color c) { }
        public void SetPixels(Color[] colors) { }
        public void SetPixels32(Color32[] colors) { }
        public Color GetPixel(int x, int y) { return Color.clear; }
        public Color[] GetPixels() { return new Color[width * height]; }
        public Color32[] GetPixels32() { return new Color32[width * height]; }
        public void Apply() { }
        public void Apply(bool updateMipmaps) { }
        public void ReadPixels(Rect source, int destX, int destY) { }
        public void ReadPixels(Rect source, int destX, int destY, bool recalculateMipMaps) { }
        public byte[] EncodeToPNG() { return new byte[0]; }
        public static Texture2D whiteTexture { get { return new Texture2D(1, 1); } }
        public static Texture2D blackTexture { get { return new Texture2D(1, 1); } }
    }
    public enum TextureFormat { Alpha8, RGB24, RGBA32, ARGB32, BGRA32 }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp, Mirror }
    public enum SpriteMeshType { FullRect, Tight }
    public enum SpriteMaskInteraction { None, VisibleInsideMask, VisibleOutsideMask }
    public class Sprite : Object
    {
        public Texture2D texture { get; set; }
        public Rect rect { get; set; }
        public Vector2 pivot { get; set; }
        public float pixelsPerUnit { get; set; }
        public Bounds bounds { get; set; }
        public static Sprite Create(Texture2D tex, Rect rect, Vector2 pivot) { return new Sprite { texture = tex, rect = rect, pivot = pivot, pixelsPerUnit = 100f }; }
        public static Sprite Create(Texture2D tex, Rect rect, Vector2 pivot, float ppu) { return new Sprite { texture = tex, rect = rect, pivot = pivot, pixelsPerUnit = ppu }; }
        public static Sprite Create(Texture2D tex, Rect rect, Vector2 pivot, float ppu, uint extrude, SpriteMeshType meshType) { return new Sprite { texture = tex, rect = rect, pivot = pivot, pixelsPerUnit = ppu }; }
    }
    public class Material : Object
    {
        public Material(Shader s) { shader = s; }
        public Material(Material m) { shader = m.shader; }
        public Shader shader { get; set; }
        public Color color { get; set; }
        public Texture mainTexture { get; set; }
        public int renderQueue { get; set; }
        public bool HasProperty(string n) { return true; }
        public void SetColor(string n, Color c) { }
        public void SetFloat(string n, float f) { }
        public void SetInt(string n, int i) { }
        public void SetTexture(string n, Texture t) { }
        public Color GetColor(string n) { return Color.white; }
        public float GetFloat(string n) { return 0f; }
    }
    public class Renderer : Component
    {
        public Material material { get; set; }
        public Material sharedMaterial { get; set; }
        public Material[] materials { get; set; }
        public Bounds bounds { get; set; }
        public bool enabled { get; set; }
        public bool receiveShadows { get; set; }
        public string sortingLayerName { get; set; }
        public int sortingOrder { get; set; }
        public Rendering.ShadowCastingMode shadowCastingMode { get; set; }
        public Rendering.LightProbeUsage lightProbeUsage { get; set; }
        public Rendering.ReflectionProbeUsage reflectionProbeUsage { get; set; }
    }
    public class SpriteRenderer : Renderer
    {
        public Sprite sprite { get; set; }
        public Color color { get; set; }
        public bool flipX { get; set; }
        public bool flipY { get; set; }
        public SpriteMaskInteraction maskInteraction { get; set; }
        public int sortingLayerID { get; set; }
    }
    public class Mesh : Object
    {
        public Vector3[] vertices { get; set; }
        public int[] triangles { get; set; }
        public Vector2[] uv { get; set; }
        public Vector2[] uv2 { get; set; }
        public Vector3[] normals { get; set; }
        public Color[] colors { get; set; }
        public Color32[] colors32 { get; set; }
        public Bounds bounds { get; set; }
        public void Clear() { }
        public void SetVertices(List<Vector3> v) { }
        public void SetTriangles(int[] t, int submesh) { }
        public void SetTriangles(List<int> t, int submesh) { }
        public void SetUVs(int channel, List<Vector2> uvs) { }
        public void SetColors(List<Color> c) { }
        public void SetColors(List<Color32> c) { }
        public void SetNormals(List<Vector3> n) { }
        public void RecalculateNormals() { }
        public void RecalculateBounds() { }
        public void UploadMeshData(bool markNoLongerReadable) { }
    }
    public class MeshFilter : Component { public Mesh mesh { get; set; } public Mesh sharedMesh { get; set; } }
    public class MeshRenderer : Renderer { }
    public class Collider : Component { public Bounds bounds { get; set; } public bool enabled { get; set; } }
    public class MeshCollider : Collider { public Mesh sharedMesh { get; set; } public bool convex { get; set; } }
    public class BoxCollider : Collider { }
    public class Rigidbody : Component { }

    public enum CameraClearFlags { Skybox = 1, Color = 2, SolidColor = 2, Depth = 3, Nothing = 4 }
    public class Camera : Behaviour
    {
        public static Camera main { get { return null; } }
        public bool orthographic { get; set; }
        public float orthographicSize { get; set; }
        public float fieldOfView { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public RenderTexture targetTexture { get; set; }
        public int cullingMask { get; set; }
        public float aspect { get; set; }
        public int pixelWidth { get { return Screen.width; } }
        public int pixelHeight { get { return Screen.height; } }
        public Rect rect { get; set; }
        public void Render() { }
        public Vector3 ScreenToWorldPoint(Vector3 p) { return p; }
        public Vector3 WorldToScreenPoint(Vector3 p) { return p; }
        public Vector3 ScreenToViewportPoint(Vector3 p) { return p; }
        public Ray ScreenPointToRay(Vector3 p) { return new Ray(); }
    }
    public struct Ray { public Vector3 origin, direction; }

    public enum LightType { Spot, Directional, Point, Area }
    public enum LightShadows { None, Hard, Soft }
    public class Light : Behaviour
    {
        public LightType type { get; set; }
        public Color color { get; set; }
        public float intensity { get; set; }
        public float range { get; set; }
        public LightShadows shadows { get; set; }
        public float shadowStrength { get; set; }
    }
    public static class RenderSettings
    {
        public static Rendering.AmbientMode ambientMode { get; set; }
        public static Color ambientLight { get; set; }
        public static Color ambientSkyColor { get; set; }
        public static Material skybox { get; set; }
        public static bool fog { get; set; }
        public static Color fogColor { get; set; }
        public static float ambientIntensity { get; set; }
    }
    public enum RenderTextureFormat { ARGB32, ARGBHalf, Depth, Default, RGB24, RGBA32 }
    public class RenderTexture : Texture
    {
        public RenderTexture(int w, int h, int depth) { width = w; height = h; }
        public RenderTexture(int w, int h, int depth, RenderTextureFormat fmt) { width = w; height = h; }
        public static RenderTexture active { get; set; }
        public bool Create() { return true; }
        public void Release() { }
        public void DiscardContents() { }
    }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public class Canvas : Behaviour
    {
        public RenderMode renderMode { get; set; }
        public int sortingOrder { get; set; }
        public Camera worldCamera { get; set; }
        public float planeDistance { get; set; }
        public bool pixelPerfect { get; set; }
        public float scaleFactor { get { return 1f; } }
    }
    public class CanvasGroup : Behaviour { public float alpha { get; set; } public bool interactable { get; set; } public bool blocksRaycasts { get; set; } }
    public class CanvasRenderer : Component { }
    public class Font : Object
    {
        public static Font CreateDynamicFontFromOSFont(string name, int size) { return new Font { name = name }; }
        public static Font CreateDynamicFontFromOSFont(string[] names, int size) { return new Font(); }
    }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public class TextAsset : Object { public string text { get { return ""; } } }
    public class ScriptableObject : Object { }

    // 属性
    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class HideInInspector : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field)] public class HeaderAttribute : Attribute { public HeaderAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field)] public class SpaceAttribute : Attribute { public SpaceAttribute() { } public SpaceAttribute(float h) { } }
    [AttributeUsage(AttributeTargets.Field)] public class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    [AttributeUsage(AttributeTargets.Class)] public class ExecuteAlways : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public class ExecuteInEditMode : Attribute { }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public class RequireComponent : Attribute { public RequireComponent(Type t) { } public RequireComponent(Type a, Type b) { } }
    [AttributeUsage(AttributeTargets.Class)] public class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public class AddComponentMenu : Attribute { public AddComponentMenu(string m) { } }
    [AttributeUsage(AttributeTargets.Method)] public class ContextMenu : Attribute { public ContextMenu(string m) { } }
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, BeforeSplashScreen, AfterAssembliesLoaded, SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)]
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeLoadType loadType { get; private set; }
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { loadType = t; }
    }
    public class SpaceAttributeAlias { }
    public static class MathfExtras { }
}

namespace UnityEngine.Rendering
{
    public enum AmbientMode { Skybox = 0, Trilight = 1, Flat = 3, Custom = 4 }
    public enum ShadowCastingMode { Off = 0, On = 1, TwoSided = 2, ShadowsOnly = 3 }
    public enum LightProbeUsage { Off = 0, BlendProxies = 1, UseProxyVolume = 2, CustomProvided = 4 }
    public enum ReflectionProbeUsage { Off = 0, BlendProxies = 1, Simple = 2, BoxProjection = 4 }
    public enum BlendMode { Zero, One, SrcAlpha, OneMinusSrcAlpha }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public string name { get { return ""; } set { } }
        public string path { get { return ""; } set { } }
        public int buildIndex { get { return -1; } }
        public bool isLoaded { get { return true; } }
        public bool IsValid() { return true; }
        public GameObject[] GetRootGameObjects() { return new GameObject[0]; }
    }
    public static class SceneManager
    {
        public static Scene GetActiveScene() { return new Scene(); }
        public static void LoadScene(string name) { }
        public static void LoadScene(int index) { }
        public static int sceneCount { get { return 1; } }
    }
}