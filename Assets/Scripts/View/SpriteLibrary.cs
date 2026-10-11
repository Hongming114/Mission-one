using System.Collections.Generic;
using UnityEngine;

namespace DungeonCity.View
{
    /// <summary>
    /// 贴图 / 精灵 / 材质缓存。占位素材全部由 PixelArt 现场生成，所以第一次取值时生成、之后复用。
    /// 真实素材接入后，只要把这里的取图实现换成 Resources.Load 即可，其它代码不用改。
    /// </summary>
    public sealed class SpriteLibrary
    {
        public static readonly string[] ShaderCandidates =
        {
            "Sprites/Default", "Unlit/Transparent", "Universal Render Pipeline/Unlit", "Unlit/Color", "Standard"
        };

        readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        public readonly Material SpriteMaterial;
        public string ShaderName = "(none)";

        public SpriteLibrary()
        {
            Shader sh = null;
            for (int i = 0; i < ShaderCandidates.Length && sh == null; i++)
            {
                sh = Shader.Find(ShaderCandidates[i]);
                if (sh != null) ShaderName = ShaderCandidates[i];
            }
            if (sh == null) sh = MaterialLibrary.BaseShader;
            SpriteMaterial = new Material(sh);
            SpriteMaterial.color = Color.white;
            SpriteMaterial.renderQueue = 3000;
            if (SpriteMaterial.HasProperty("_Color")) SpriteMaterial.SetColor("_Color", Color.white);
            if (SpriteMaterial.HasProperty("_BaseColor")) SpriteMaterial.SetColor("_BaseColor", Color.white);
        }

        Texture2D Get(string key, System.Func<Texture2D> make)
        {
            Texture2D t;
            if (texCache.TryGetValue(key, out t) && t != null) return t;
            t = make();
            texCache[key] = t;
            return t;
        }

        Sprite GetSprite(string key, Vector2 pivot, System.Func<Texture2D> make)
        {
            Sprite s;
            if (spriteCache.TryGetValue(key, out s) && s != null) return s;
            s = PixelArt.ToSprite(Get(key, make), pivot);
            spriteCache[key] = s;
            return s;
        }

        // ---------------- 主角 ----------------
        public Texture2D PlayerTexture(int dirIndex, int frame)
        {
            return Get($"player_{dirIndex}_{frame}", () => PixelArt.PlayerFrame(dirIndex, frame));
        }

        public Sprite Player(int dirIndex, int frame)
        {
            return GetSprite($"player_{dirIndex}_{frame}", new Vector2(0.5f, 0f), () => PixelArt.PlayerFrame(dirIndex, frame));
        }

        public Sprite PlayerGhost(int dirIndex, int frame)
        {
            return GetSprite($"pghost_{dirIndex}_{frame}", new Vector2(0.5f, 0f),
                () => PixelArt.Tinted(PixelArt.PlayerFrame(dirIndex, frame), new Color(0.62f, 0.82f, 1f, 0.55f)));
        }

        public Sprite Sword() { return GetSprite("sword", new Vector2(0f, 0.5f), PixelArt.Sword); }

        public Sprite SwordGhost()
        {
            return GetSprite("sword_ghost", new Vector2(0f, 0.5f),
                () => PixelArt.Tinted(PixelArt.Sword(), new Color(1f, 1f, 1f, 0.35f)));
        }

        public Sprite PlayerHurtFrame(int dirIndex, int frame)
        {
            return GetSprite($"phurt_{dirIndex}_{frame}", new Vector2(0.5f, 0f),
                () => PixelArt.Tinted(PixelArt.PlayerFrame(dirIndex, frame), new Color(1f, 0.5f, 0.5f, 1f)));
        }

        /// <summary>跳跃姿势（文档 L32）：双腿收起分开，配合 PlayerView 抬高广告牌。</summary>
        public Sprite PlayerJump(int dirIndex)
        {
            return GetSprite($"pjump_{dirIndex}", new Vector2(0.5f, 0f), () => PixelArt.PlayerJump(dirIndex));
        }

        // ---------------- 史莱姆 ----------------
        public Texture2D SlimeTexture(int frame)
        {
            return Get($"slime_{frame}", () => PixelArt.SlimeFrame(frame));
        }

        public Sprite Slime(int frame)
        {
            return GetSprite($"slime_{frame}", new Vector2(0.5f, 0f), () => PixelArt.SlimeFrame(frame));
        }

        public Sprite SlimeHurt()
        {
            return GetSprite("slime_hurt", new Vector2(0.5f, 0f), PixelArt.SlimeHurt);
        }

        /// <summary>史莱姆清（文档 L43）：10x10px 的果冻块，约 0.6 格，锚点在中心。</summary>
        public Sprite SlimeJelly()
        {
            return GetSprite("slime_jelly", new Vector2(0.5f, 0.5f), PixelArt.SlimeJelly);
        }

        /// <summary>背包图标（调试3-4）：血条下方那个小背包按钮用（16x16px）。</summary>
        public Sprite BackpackIcon()
        {
            return GetSprite("backpack_icon", new Vector2(0.5f, 0.5f), PixelArt.BackpackIcon);
        }

        // ---------------- 特效 / UI ----------------
        public Sprite Shadow() { return GetSprite("shadow", new Vector2(0.5f, 0.5f), PixelArt.Shadow); }

        public Sprite Sector()
        {
            return GetSprite("sector", new Vector2(0.5f, 0.5f),
                () => PixelArt.Sector(PixelArt.PPU, 75f, new Color(1f, 1f, 0.85f, 0.5f)));
        }

        public Sprite Blast()
        {
            return GetSprite("blast", new Vector2(0.5f, 0.5f), () => PixelArt.Circle(24, new Color(1f, 0.95f, 0.6f, 0.8f)));
        }

        public Sprite BarFill()
        {
            return GetSprite("bar_fill", new Vector2(0f, 0.5f), () => PixelArt.BarFill(64, 12, new Color(0.90f, 0.18f, 0.16f, 1f)));
        }

        public Sprite BarBack()
        {
            return GetSprite("bar_back", new Vector2(0f, 0.5f), () => PixelArt.BarFill(64, 12, new Color(0.12f, 0.10f, 0.12f, 0.85f)));
        }

        public Sprite White() { return GetSprite("white", new Vector2(0.5f, 0.5f), () => PixelArt.Solid(4, 4, Color.white)); }
    }
}
