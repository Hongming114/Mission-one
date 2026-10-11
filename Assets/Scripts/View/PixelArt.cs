using System.Collections.Generic;
using UnityEngine;

namespace DungeonCity.View
{
    /// <summary>
    /// 程序化像素美术（占位素材，等真实素材接入后替换）：
    ///   * 主角 16x16（1 个方格 = 16 像素），8 个方向 x 3 帧（静止 + 2 帧腿部行走），
    ///     只画 5 个基准方向（下/左下/左/左上/上），其余 3 个方向水平镜像；
    ///   * 剑 16x5（长度正好 1 个方格），锚点在左端（握把）；
    ///   * 史莱姆 13x11（约 0.8 方格），4 帧蠕动，另有"受击变红"版本；
    ///   * 影子、刀光扇形、血条贴图。
    /// 贴图全部 Point 过滤、无 mipmap、Clamp，保证像素清晰。
    /// </summary>
    public static class PixelArt
    {
        public const int PPU = 16;       // 每个方格 16 像素

        // 调色板
        public static readonly Color32 Skin = new Color32(240, 200, 165, 255);
        public static readonly Color32 SkinDark = new Color32(205, 160, 125, 255);
        public static readonly Color32 Hair = new Color32(120, 78, 45, 255);
        public static readonly Color32 HairDark = new Color32(85, 52, 28, 255);
        public static readonly Color32 Shirt = new Color32(70, 120, 190, 255);
        public static readonly Color32 ShirtDark = new Color32(45, 82, 140, 255);
        public static readonly Color32 Pants = new Color32(70, 72, 96, 255);
        public static readonly Color32 PantsDark = new Color32(45, 46, 62, 255);
        public static readonly Color32 Boot = new Color32(60, 42, 30, 255);
        public static readonly Color32 Steel = new Color32(226, 232, 240, 255);
        public static readonly Color32 SteelDark = new Color32(150, 158, 172, 255);
        public static readonly Color32 Hilt = new Color32(140, 96, 50, 255);
        public static readonly Color32 Eye = new Color32(30, 26, 34, 255);
        public static readonly Color32 SlimeBody = new Color32(88, 214, 118, 255);
        public static readonly Color32 SlimeDark = new Color32(44, 148, 74, 255);
        public static readonly Color32 SlimeLight = new Color32(168, 245, 184, 255);

        public static Texture2D New(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            t.anisoLevel = 0;
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            t.SetPixels32(px);
            return t;
        }

        public static void Px(Texture2D t, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= t.width || y >= t.height) return;
            t.SetPixel(x, y, c);
        }

        public static void Fill(Texture2D t, int x, int y, int w, int h, Color32 c)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++) Px(t, xx, yy, c);
        }

        public static Texture2D Solid(int w, int h, Color c)
        {
            var t = New(w, h);
            Fill(t, 0, 0, w, h, (Color32)c);
            t.Apply();
            return t;
        }

        /// <summary>染色副本（残影 / 受击闪红用）。</summary>
        public static Texture2D Tinted(Texture2D src, Color tint, float alphaScale = 1f)
        {
            var t = New(src.width, src.height);
            var src32 = src.GetPixels32();
            var out32 = new Color32[src32.Length];
            for (int i = 0; i < src32.Length; i++)
            {
                Color32 s = src32[i];
                out32[i] = new Color32((byte)(s.r * tint.r), (byte)(s.g * tint.g), (byte)(s.b * tint.b),
                                       (byte)Mathf.Clamp(s.a * tint.a * alphaScale, 0f, 255f));
            }
            t.SetPixels32(out32);
            t.Apply();
            return t;
        }

        /// <summary>水平镜像（8 方向里只画 5 个基准方向，剩下 3 个靠镜像）。</summary>
        public static Texture2D MirrorX(Texture2D src)
        {
            var t = New(src.width, src.height);
            for (int y = 0; y < src.height; y++)
                for (int x = 0; x < src.width; x++)
                    Px(t, src.width - 1 - x, y, src.GetPixel(x, y));
            t.Apply();
            return t;
        }

        // ============================================================ 主角

        /// <summary>方向序号：0 下、1 左下、2 左、3 左上、4 上、5 右上、6 右、7 右下。</summary>
        public static bool IsMirrored(int dirIndex) { return dirIndex == 5 || dirIndex == 6 || dirIndex == 7; }

        /// <summary>镜像前的基准方向：0 下、1 左下、2 左、3 左上、4 上。</summary>
        public static int BaseDir(int dirIndex)
        {
            switch (dirIndex)
            {
                case 0: return 0;
                case 1: return 1;
                case 2: return 2;
                case 3: return 3;
                case 4: return 4;
                case 5: return 3;
                case 6: return 2;
                default: return 1;
            }
        }

        public static Texture2D PlayerFrame(int dirIndex, int frame)
        {
            int baseDir = BaseDir(dirIndex);
            Texture2D t = New(16, 16);
            int bob = frame == 1 ? 1 : 0;                 // 行走时躯干轻微上下
            DrawLegs(t, baseDir, frame);
            DrawTorso(t, baseDir, bob);
            DrawArms(t, baseDir, bob, frame);
            DrawHead(t, baseDir, bob);
            t.Apply();
            return IsMirrored(dirIndex) ? MirrorX(t) : t;
        }

        // kind: 0 正面(下)  1 左下  2 侧面(左)  3 左上  4 背面(上)
        static void DrawLegs(Texture2D t, int kind, int frame)
        {
            int lift = frame == 1 ? 1 : 0;
            int drop = frame == 2 ? 1 : 0;
            if (kind == 0 || kind == 4)
            {
                Fill(t, 5, 0, 2, 4 - lift, PantsDark);   // 左腿
                Fill(t, 5, 0, 2, 1, Boot);
                Fill(t, 9, 0, 2, 4 - drop, Pants);       // 右腿
                Fill(t, 9, 0, 2, 1, Boot);
            }
            else if (kind == 2)
            {
                Fill(t, 5, 0, 3, 4 - lift, PantsDark);   // 前腿
                Fill(t, 5, 0, 3, 1, Boot);
                Fill(t, 8, 0, 3, 4 - drop, Pants);       // 后腿
                Fill(t, 8, 0, 3, 1, Boot);
            }
            else if (kind == 1)
            {
                Fill(t, 4, 0, 2, 4 - lift, PantsDark);
                Fill(t, 4, 0, 2, 1, Boot);
                Fill(t, 9, 0, 3, 4 - drop, Pants);
                Fill(t, 9, 0, 3, 1, Boot);
            }
            else
            {
                Fill(t, 4, 0, 3, 4 - drop, Pants);
                Fill(t, 4, 0, 3, 1, Boot);
                Fill(t, 9, 0, 2, 4 - lift, PantsDark);
                Fill(t, 9, 0, 2, 1, Boot);
            }
        }

        static void DrawTorso(Texture2D t, int kind, int bob)
        {
            int y = 4 + bob;
            int x0 = kind == 0 || kind == 4 ? 4 : (kind == 2 ? 5 : 4);
            int w = kind == 0 || kind == 4 ? 8 : (kind == 2 ? 6 : 7);
            Fill(t, x0, y, w, 7, Shirt);
            Fill(t, x0, y, w, 1, ShirtDark);          // 腰带
            Fill(t, x0, y + 6, w, 1, ShirtDark);      // 领口
            Fill(t, x0 + 2, y + 2, 1, 4, ShirtDark);  // 衣褶
        }

        static void DrawArms(Texture2D t, int kind, int bob, int frame)
        {
            int y = 5 + bob + (frame == 2 ? 1 : 0);
            if (kind == 0 || kind == 4)
            {
                Fill(t, 2, y, 2, 5, Skin);
                Fill(t, 2, y - 1, 2, 1, SkinDark);
                Fill(t, 12, y, 2, 5, Skin);
                Fill(t, 12, y - 1, 2, 1, SkinDark);
            }
            else if (kind == 2)
            {
                Fill(t, 3, y, 2, 6, Skin);            // 前手（持剑手）
                Fill(t, 10, y, 2, 5, SkinDark);       // 后手
            }
            else if (kind == 1)
            {
                Fill(t, 3, y - 1, 2, 6, Skin);
                Fill(t, 11, y, 2, 5, SkinDark);
            }
            else
            {
                Fill(t, 3, y + 1, 2, 5, SkinDark);
                Fill(t, 11, y, 2, 6, Skin);
            }
        }

        static void DrawHead(Texture2D t, int kind, int bob)
        {
            int y = 11 + bob;
            int x0 = kind == 0 || kind == 4 ? 4 : (kind == 2 ? 5 : 4);
            int w = kind == 0 || kind == 4 ? 8 : 6;
            Fill(t, x0, y, w, 4, Skin);                  // 脸
            Fill(t, x0, y + 3, w, 2, Hair);              // 头发（顶部）
            Fill(t, x0 - 1, y + 3, 1, 2, HairDark);
            Fill(t, x0 + w, y + 3, 1, 2, HairDark);
            if (kind == 0)
            {
                Fill(t, x0 + 1, y + 1, 2, 1, Eye);
                Fill(t, x0 + w - 3, y + 1, 2, 1, Eye);
            }
            else if (kind == 1)
            {
                Fill(t, x0 + 1, y + 1, 2, 1, Eye);
                Fill(t, x0 + w - 3, y + 1, 1, 1, SkinDark);
            }
            else if (kind == 2)
            {
                Fill(t, x0 + 1, y + 2, 2, 1, Eye);
                Fill(t, x0 + w - 1, y + 2, 1, 2, HairDark);   // 侧面：后脑勺头发
            }
            else if (kind == 3)
            {
                Fill(t, x0 + 2, y + 2, 1, 1, Eye);
                Fill(t, x0, y + 1, w, 2, HairDark);
            }
            else
            {
                Fill(t, x0, y + 1, w, 2, HairDark);          // 背面：全是头发
            }
        }

        /// <summary>跳跃姿势（文档 L32）：双腿收起并分开，配合视图把广告牌抬高 1.5 格（阴影留在原地）。</summary>
        public static Texture2D PlayerJump(int dirIndex)
        {
            int kind = BaseDir(dirIndex);
            Texture2D t = New(16, 16);
            DrawJumpLegs(t, kind);
            DrawTorso(t, kind, 1);
            DrawArms(t, kind, 1, 0);
            DrawHead(t, kind, 1);
            t.Apply();
            return IsMirrored(dirIndex) ? MirrorX(t) : t;
        }

        // 跳跃时的腿：比站立姿势高 1~2 px、左右分开（落地才合拢）
        static void DrawJumpLegs(Texture2D t, int kind)
        {
            if (kind == 0 || kind == 4)
            {
                Fill(t, 4, 2, 2, 4, PantsDark);   // 左腿（收起）
                Fill(t, 4, 2, 2, 1, Boot);
                Fill(t, 10, 2, 2, 4, Pants);      // 右腿（分开）
                Fill(t, 10, 2, 2, 1, Boot);
            }
            else if (kind == 2)
            {
                Fill(t, 4, 2, 3, 4, PantsDark);
                Fill(t, 4, 2, 3, 1, Boot);
                Fill(t, 9, 3, 3, 3, Pants);
                Fill(t, 9, 3, 3, 1, Boot);
            }
            else if (kind == 1)
            {
                Fill(t, 3, 2, 2, 4, PantsDark);
                Fill(t, 3, 2, 2, 1, Boot);
                Fill(t, 10, 3, 2, 3, Pants);
                Fill(t, 10, 3, 2, 1, Boot);
            }
            else
            {
                Fill(t, 3, 3, 2, 3, Pants);
                Fill(t, 3, 3, 2, 1, Boot);
                Fill(t, 10, 2, 2, 4, PantsDark);
                Fill(t, 10, 2, 2, 1, Boot);
            }
        }

        /// <summary>剑：16x5，长度 = 1 个方格，锚点在最左端（握把）。</summary>
        public static Texture2D Sword()
        {
            var t = New(16, 5);
            Fill(t, 0, 1, 3, 3, Hilt);          // 握把
            Fill(t, 3, 0, 1, 5, SteelDark);     // 护手
            Fill(t, 4, 1, 11, 3, Steel);        // 剑身
            Fill(t, 4, 1, 11, 1, SteelDark);    // 剑刃
            Fill(t, 15, 2, 1, 1, Steel);        // 剑尖
            t.Apply();
            return t;
        }

        // ============================================================ 史莱姆

        /// <summary>13x11 的史莱姆，frame 0..3 为蠕动帧（越后面越扁越宽）。</summary>
        public static Texture2D SlimeFrame(int frame)
        {
            var t = New(13, 11);
            int h = 11 - (frame == 1 ? 2 : (frame == 2 ? 3 : (frame == 3 ? 1 : 0)));
            int sq = frame == 2 ? 1 : 0;                 // 压扁时向两侧鼓
            for (int y = 0; y < h; y++)
            {
                float u = (float)y / Mathf.Max(1, h - 1);
                int half = Mathf.RoundToInt((5.2f + sq * 0.6f) * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u * 0.92f)));
                int x0 = 6 - half, w = half * 2 + 1;
                Color32 body = y >= h - 3 ? SlimeLight : SlimeBody;
                Fill(t, x0, y, w, 1, body);
                Px(t, x0, y, SlimeDark);
                Px(t, x0 + w - 1, y, SlimeDark);
            }
            // 底边描边 + 眼睛（正面）
            Fill(t, 5 - sq, 0, 9 + sq * 2, 1, SlimeDark);
            int ey = Mathf.Max(2, h / 2 + 1);
            Px(t, 4, ey, Eye); Px(t, 4, ey + 1, Eye);
            Px(t, 8, ey, Eye); Px(t, 8, ey + 1, Eye);
            Px(t, 3, ey + 2, SlimeDark); Px(t, 9, ey + 2, SlimeDark);
            t.Apply();
            return t;
        }

        public static Texture2D SlimeHurt() { return Tinted(SlimeFrame(0), new Color(1f, 0.45f, 0.45f, 1f)); }
        /// <summary>史莱姆清（文档 L43）：10x10 px（= 0.6 格）的青绿果冻块，参考原神史莱姆凝液的造型。</summary>
        public static Texture2D SlimeJelly()
        {
            var t = New(10, 10);
            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    float dx = (x - 4.5f) / 4.2f;
                    float dy = (y - 4.6f) / 4.0f;
                    float d = dx * dx + dy * dy;
                    if (d > 1f) continue;
                    if (d > 0.72f) { Px(t, x, y, SlimeDark); continue; }             // 外圈描边
                    if (y >= 6 && d < 0.34f) { Px(t, x, y, SlimeLight); continue; }  // 顶部高光
                    if (y <= 2) { Px(t, x, y, new Color32(28, 116, 62, 255)); continue; } // 底部沉色
                    Px(t, x, y, SlimeBody);
                }
            }
            Px(t, 3, 4, SlimeLight); Px(t, 4, 4, SlimeLight);   // 内部小气泡
            Px(t, 6, 6, SlimeLight); Px(t, 7, 6, SlimeLight);
            t.Apply();
            return t;
        }

        /// <summary>背包图标（调试3-4）：16x16 的小背包，锚点中心，用于血条下方的按钮。</summary>
        public static Texture2D BackpackIcon()
        {
            var t = New(16, 16);
            Color32 body = new Color32(158, 110, 62, 255);     // 皮革主色
            Color32 flap = new Color32(112, 76, 42, 255);      // 顶盖 / 袋口
            Color32 strap = new Color32(74, 50, 30, 255);      // 背带 / 描边
            Color32 buckle = new Color32(226, 186, 88, 255);   // 铜扣
            for (int y = 1; y <= 14; y++)
            {
                for (int x = 2; x <= 13; x++)
                {
                    bool corner = (x == 2 || x == 13) && (y <= 2 || y >= 13);
                    if (corner) continue;
                    if (x == 2 || x == 13 || y == 1 || y == 14) { Px(t, x, y, strap); continue; }   // 描边
                    if (y >= 11) { Px(t, x, y, flap); continue; }                                    // 顶盖
                    Px(t, x, y, body);
                }
            }
            for (int y = 3; y <= 10; y++) { Px(t, 5, y, strap); Px(t, 10, y, strap); }               // 两条背带
            for (int y = 6; y <= 8; y++) { for (int x = 7; x <= 8; x++) Px(t, x, y, buckle); }        // 铜扣
            for (int x = 4; x <= 11; x++) Px(t, x, 9, flap);                                         // 袋口
            t.Apply();
            return t;
        }

        // ============================================================ 特效 / UI

        /// <summary>影子：16x7 的椭圆，越靠边越透明。</summary>
        public static Texture2D Shadow()
        {
            var t = New(16, 7);
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 16; x++)
                {
                    float dx = (x - 7.5f) / 7.5f, dy = (y - 3f) / 3f;
                    float d = dx * dx + dy * dy;
                    if (d > 1f) continue;
                    byte a = (byte)(70f * (1f - d));
                    Px(t, x, y, new Color32(0, 0, 0, a));
                }
            t.Apply();
            return t;
        }

        /// <summary>刀光：半径 radiusPx、圆心角 2*halfAngleDeg 的扇形（锚点在圆心）。</summary>
        public static Texture2D Sector(float radiusPx, float halfAngleDeg, Color color)
        {
            int size = Mathf.CeilToInt(radiusPx) * 2 + 1;
            var t = New(size, size);
            float cx = size * 0.5f - 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx, dy = y - cx;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > radiusPx || dist < 0.6f) continue;
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (Mathf.Abs(ang) > halfAngleDeg) continue;
                    float edge = Mathf.Clamp01(1f - dist / radiusPx);
                    Color c = color;
                    c.a = color.a * (0.35f + 0.65f * edge);
                    Px(t, x, y, c);
                }
            t.Apply();
            return t;
        }

        /// <summary>实心圆（扑击/受击特效）。</summary>
        public static Texture2D Circle(int size, Color color)
        {
            var t = New(size, size);
            float c = size * 0.5f - 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / (size * 0.5f);
                    if (d > 1f) continue;
                    Color col = color;
                    col.a = color.a * Mathf.Clamp01(1f - d * d);
                    Px(t, x, y, col);
                }
            t.Apply();
            return t;
        }

        /// <summary>血条填充：红色，左侧圆角（锚点在左中）。</summary>
        public static Texture2D BarFill(int w, int h, Color color)
        {
            var t = New(w, h);
            Fill(t, 0, 0, w, h, (Color32)color);
            Fill(t, 0, 0, w, 1, (Color32)new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 1f));
            Fill(t, 0, h - 1, w, 1, (Color32)new Color(color.r * 1.15f, color.g * 1.15f, color.b * 1.15f, 1f));
            t.Apply();
            return t;
        }

        public static Sprite ToSprite(Texture2D t, Vector2 pivot)
        {
            return Sprite.Create(t, new Rect(0f, 0f, t.width, t.height), pivot, PPU, 0, SpriteMeshType.FullRect);
        }

        public static Sprite ToSprite(Texture2D t) { return ToSprite(t, new Vector2(0.5f, 0f)); }
    }
}