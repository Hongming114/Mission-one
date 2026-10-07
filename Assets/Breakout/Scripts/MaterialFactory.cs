using UnityEngine;

namespace Breakout
{
    /// <summary>
    /// 渲染管线无关的材质工厂。
    ///
    /// 为什么要它：URP 工程里 Shader.Find("Standard") 返回 null，内置管线工程里
    /// 又没有 "Universal Render Pipeline/Lit"，写死任何一个都会在另一种工程里变成洋红色。
    /// 这里从上到下挨个试，两种管线都能跑。
    /// </summary>
    public static class MaterialFactory
    {
        static Shader _cached;

        public static Shader BaseShader
        {
            get
            {
                if (_cached != null) return _cached;
                string[] candidates =
                {
                    "Universal Render Pipeline/Lit",
                    "Standard",
                    "Universal Render Pipeline/Unlit",
                    "Unlit/Color",
                };
                foreach (string n in candidates)
                {
                    Shader s = Shader.Find(n);
                    if (s != null) { _cached = s; return s; }
                }
                Debug.LogError("[打砖块] 一个可用 Shader 都找不到，物体会是洋红色");
                return null;
            }
        }

        public static Material Solid(Color color, float smoothness = 0.4f, float metallic = 0f, bool emissive = false)
        {
            Material m = new Material(BaseShader);
            m.name = "Mat_" + ColorUtility.ToHtmlStringRGB(color);
            if (m.HasProperty("_BaseColor"))  m.SetColor("_BaseColor", color);   // URP
            if (m.HasProperty("_Color"))      m.SetColor("_Color", color);       // 内置管线
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic", metallic);
            if (emissive && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 0.7f);
            }
            return m;
        }

        /// <summary>用 MaterialPropertyBlock 单独改一个物体的颜色（不影响共用同一个材质的其它物体）。</summary>
        public static void SetColor(Renderer r, Color c)
        {
            if (r == null) return;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            r.SetPropertyBlock(block);
        }
    }
}