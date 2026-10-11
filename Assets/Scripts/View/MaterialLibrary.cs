using System.Collections.Generic;
using UnityEngine;
using DungeonCity.Core;

namespace DungeonCity.View
{
    /// <summary>
    /// 材质工厂：兼容内置渲染管线 / URP —— 依次尝试可用的 Shader，避免出现粉色（丢失着色器）。
    /// </summary>
    public class MaterialLibrary
    {
        private static readonly string[] ShaderCandidates =
        {
            "Standard",
            "Universal Render Pipeline/Lit",
            "Legacy Shaders/Diffuse",
            "Unlit/Color",
            "Sprites/Default",
        };

        private static Shader _shader;
        private static string _shaderName = "(未找到)";
        private readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();
        public readonly List<Material> Created = new List<Material>();

        public static string ShaderName => _shaderName;

        public static Shader BaseShader
        {
            get
            {
                if (_shader != null) return _shader;
                foreach (string n in ShaderCandidates)
                {
                    Shader s = Shader.Find(n);
                    if (s != null) { _shader = s; _shaderName = n; return _shader; }
                }
                _shader = Shader.Find("Hidden/InternalErrorShader");
                _shaderName = _shader != null ? "Hidden/InternalErrorShader" : "(null)";
                return _shader;
            }
        }

        public Material Get(string key, Color color, float smoothness = 0.05f)
        {
            Material m;
            if (_cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(BaseShader) { name = key };
            if (m.HasProperty("_Color")) m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            _cache[key] = m;
            Created.Add(m);
            return m;
        }

        public Material Floor(Color c) => Get("地板" + ColorKey(c), c);
        public Material Wall(Color c) => Get("墙" + ColorKey(c), c);
        public Material Door(Color c) => Get("门" + ColorKey(c), c, 0.25f);
        public Material Ladder(Color c) => Get("梯子" + ColorKey(c), c, 0.2f);

        public Material ForTile(RoomProfile p, int tile, bool altFloor)
        {
            switch ((TileCode)tile)
            {
                case TileCode.Wall: return Wall(p.wallColor);
                case TileCode.Door: return Door(p.doorColor);
                case TileCode.Ladder: return Ladder(p.ladderColor);
                default: return Floor(altFloor ? p.floorAltColor : p.floorColor);
            }
        }

        private static string ColorKey(Color c)
        {
            return Mathf.RoundToInt(c.r * 255f) + "_" + Mathf.RoundToInt(c.g * 255f) + "_" + Mathf.RoundToInt(c.b * 255f);
        }
    }
}