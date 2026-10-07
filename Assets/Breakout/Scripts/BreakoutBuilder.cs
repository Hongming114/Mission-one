using UnityEngine;
using UnityEngine.Rendering;

namespace Breakout
{
    /// <summary>
    /// 用纯代码把一个完整的打砖块场景搭出来。
    ///
    /// 编辑器菜单（打砖块/…）和运行时自动引导都调用这里，好处是
    /// "手动搭出来的场景"和"代码搭出来的场景"永远是同一个东西 —— 你在编辑器里
    /// 一时手滑删了某个物体，删掉重跑一次就恢复了。
    ///
    /// 场景里所有东西都躺在 y = PlaneY 这一层（相当于把 2D 游戏竖起来放平），
    /// 只有这个平面上有物理碰撞。
    /// </summary>
    public static class BreakoutBuilder
    {
        public const string RootName = "[打砖块]";

        // ---------- 场地尺寸 ----------
        public const float PlaneY      = 0.5f;    // 所有物件所在的平面高度
        public const float HalfWidth   = 7.0f;    // 左右墙内壁到中线的距离
        public const float BackZ       = 6.6f;    // 后墙内壁
        public const float DeathZ      = -8.0f;   // 球掉出这个 z 就算丢了
        public const float PaddleZ     = -5.6f;
        public const float PaddleSizeX = 2.8f;
        public const int   StartLives  = 3;

        // ---------- 砖块阵列 ----------
        public const int   Cols         = 8;
        public const int   Rows         = 5;
        public const float BrickSizeX   = 1.5f;
        public const float BrickSizeY   = 0.6f;
        public const float BrickSizeZ   = 0.6f;
        public const float BrickGap     = 0.12f;
        public const float BrickFirstZ  = 5.4f;
        public const float BrickRowStep = 0.76f;

        static readonly Color[] RowColors =
        {
            new Color(0.90f, 0.25f, 0.28f),   // 红
            new Color(0.96f, 0.56f, 0.20f),   // 橙
            new Color(0.95f, 0.84f, 0.24f),   // 黄
            new Color(0.36f, 0.78f, 0.42f),   // 绿
            new Color(0.32f, 0.62f, 0.92f),   // 蓝
        };

        static readonly int[] RowPoints = { 50, 40, 30, 20, 10 };

        // =====================================================================

        public static GameManager Build()
        {
            GameObject old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            GameObject rootGo = new GameObject(RootName);
            Transform root = rootGo.transform;

            SetupEnvironment();
            SetupCamera();
            SetupLight(root);

            GameManager game = rootGo.AddComponent<GameManager>();
            game.startingLives = StartLives;
            game.deathZ = DeathZ;

            PaddleController paddle = BuildPaddle(root);
            BallController ball = BuildBall(root);

            GameObject hudGo = new GameObject("HUD");
            hudGo.transform.SetParent(root, false);
            BreakoutHud hud = hudGo.AddComponent<BreakoutHud>();
            hud.game = game;

            GameObject bricksGo = new GameObject("Bricks");
            bricksGo.transform.SetParent(root, false);
            int count = BuildBricks(game, bricksGo.transform);

            BuildFloor(root);
            BuildWalls(root);

            game.Configure(paddle, ball, hud, bricksGo.transform, count);
            return game;
        }

        // ---------------- 环境 / 相机 / 灯光 ----------------

        static void SetupEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.39f, 0.48f);
            RenderSettings.fog = false;
            QualitySettings.antiAliasing = 4;
        }

        static Camera SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                GameObject go = new GameObject("BreakoutCamera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            Transform t = cam.transform;
            t.position = new Vector3(0f, 12.5f, -9.5f);
            t.rotation = Quaternion.LookRotation(new Vector3(0f, PlaneY, 0.2f) - t.position, Vector3.up);

            cam.orthographic = false;
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.055f, 0.06f, 0.085f);
            return cam;
        }

        static void SetupLight(Transform root)
        {
            Light light = Object.FindAnyObjectByType<Light>();
            if (light == null || light.type != LightType.Directional)
            {
                GameObject go = new GameObject("BreakoutLight");
                go.transform.SetParent(root, false);
                light = go.AddComponent<Light>();
                light.type = LightType.Directional;
            }
            light.transform.rotation = Quaternion.Euler(58f, -32f, 0f);
            light.color = new Color(1f, 0.97f, 0.92f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.7f;
        }

        // ---------------- 场地 ----------------

        static void BuildFloor(Transform root)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Floor";
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0f, -0.15f, (BackZ + DeathZ) * 0.5f);
            go.transform.localScale = new Vector3(HalfWidth * 2f + 2.4f, 0.3f, BackZ - DeathZ + 2.4f);

            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = MaterialFactory.Solid(new Color(0.10f, 0.11f, 0.15f), 0.25f);
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void BuildWalls(Transform root)
        {
            float depth = BackZ - DeathZ;
            float midZ = (BackZ + DeathZ) * 0.5f;

            MakeWall(root, "WallLeft",  new Vector3(-(HalfWidth + 0.4f), PlaneY, midZ), new Vector3(0.8f, 0.8f, depth));
            MakeWall(root, "WallRight", new Vector3(HalfWidth + 0.4f, PlaneY, midZ),  new Vector3(0.8f, 0.8f, depth));
            MakeWall(root, "WallBack",  new Vector3(0f, PlaneY, BackZ + 0.4f),         new Vector3(HalfWidth * 2f + 1.6f, 0.8f, 0.8f));
        }

        static void MakeWall(Transform root, string name, Vector3 center, Vector3 size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Solid(new Color(0.17f, 0.19f, 0.26f), 0.3f);
        }

        // ---------------- 挡板 / 球 ----------------

        static PaddleController BuildPaddle(Transform root)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Paddle";
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0f, PlaneY, PaddleZ);
            go.transform.localScale = new Vector3(PaddleSizeX, 0.6f, 0.6f);
            go.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Solid(new Color(0.85f, 0.88f, 0.95f), 0.75f, 0.1f);

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

            go.GetComponent<Collider>().sharedMaterial = Bouncy();

            PaddleController pad = go.AddComponent<PaddleController>();
            pad.minX = -(HalfWidth - PaddleSizeX * 0.5f);
            pad.maxX = HalfWidth - PaddleSizeX * 0.5f;
            return pad;
        }

        static BallController BuildBall(Transform root)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Ball";
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0f, PlaneY, PaddleZ + 0.9f);
            go.transform.localScale = Vector3.one * 0.7f;
            go.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Solid(new Color(1f, 0.93f, 0.55f), 0.85f, 0f, true);
            go.GetComponent<Collider>().sharedMaterial = Bouncy();
            return go.AddComponent<BallController>();
        }

        static PhysicsMaterial _bouncy;

        /// <summary>完全弹性的物理材质：bounciness = 1、摩擦力 0，这样球碰到墙不会慢慢减速。</summary>
        static PhysicsMaterial Bouncy()
        {
            if (_bouncy != null) return _bouncy;
            _bouncy = new PhysicsMaterial("BreakoutBouncy");
            _bouncy.bounciness = 1f;
            _bouncy.dynamicFriction = 0f;
            _bouncy.staticFriction = 0f;
            _bouncy.bounceCombine = PhysicsMaterialCombine.Maximum;
            _bouncy.frictionCombine = PhysicsMaterialCombine.Minimum;
            return _bouncy;
        }

        // ---------------- 砖块 ----------------

        /// <summary>在 parent 下面铺一整面砖墙，返回砖块数量。GameManager.Restart() 也调它。</summary>
        public static int BuildBricks(GameManager game, Transform parent)
        {
            if (parent == null) return 0;

            Material[] mats = new Material[Rows];
            for (int r = 0; r < Rows; r++)
                mats[r] = MaterialFactory.Solid(RowColors[r % RowColors.Length], 0.6f, 0f, true);

            float totalW = Cols * BrickSizeX + (Cols - 1) * BrickGap;
            float startX = -totalW * 0.5f + BrickSizeX * 0.5f;

            int count = 0;
            for (int r = 0; r < Rows; r++)
            {
                float z = BrickFirstZ - r * BrickRowStep;
                for (int c = 0; c < Cols; c++)
                {
                    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Brick_" + r + "_" + c;
                    go.transform.SetParent(parent, false);
                    go.transform.position = new Vector3(startX + c * (BrickSizeX + BrickGap), PlaneY, z);
                    go.transform.localScale = new Vector3(BrickSizeX, BrickSizeY, BrickSizeZ);
                    go.GetComponent<Renderer>().sharedMaterial = mats[r];

                    Brick b = go.AddComponent<Brick>();
                    b.Init(game, RowPoints[r % RowPoints.Length], RowColors[r % RowColors.Length]);
                    count++;
                }
            }
            return count;
        }
    }
}