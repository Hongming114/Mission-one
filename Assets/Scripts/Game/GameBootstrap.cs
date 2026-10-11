using UnityEngine;
using UnityEngine.Rendering;
using DungeonCity.Core;
using DungeonCity.Sim;
using DungeonCity.UI;
using DungeonCity.View;

namespace DungeonCity.Game
{
    /// <summary>游戏入口：空场景也能跑（运行时自动建根节点 -> 主界面 -> 开始游戏）。</summary>
    public class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance;

        public GameSession session;
        public CameraRig rig;
        public MainMenuUI menu;
        public HudUI hud;
        public BloodBarUI blood;
        public MinimapUI minimap;
        public InventoryUI inventory;   // 调试3-4：5x10 背包界面（按 B 或点血条下方图标开关）
        public bool started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (FindFirstObjectByType<GameBootstrap>() != null) return;
            var go = new GameObject("游戏根");
            go.AddComponent<GameBootstrap>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            SetupEnvironment();
            rig = CameraRig.Create("主相机");
            menu = MainMenuUI.Create(this);
        }

        void SetupEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.70f);
            RenderSettings.fog = false;
            if (FindFirstObjectByType<Light>() == null)
            {
                var lgo = new GameObject("平行光");
                lgo.transform.SetParent(transform, false);
                lgo.transform.rotation = Quaternion.Euler(52f, -34f, 0f);
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Directional;
                l.color = new Color(1f, 0.97f, 0.9f);
                l.intensity = 1.15f;
                l.shadows = LightShadows.Soft;
            }
        }

        public void StartGame()
        {
            if (started) return;
            session = new GameSession();
            session.Build(transform, rig.cam);
            rig.SetTarget(session.playerView.transform, true);
            hud = HudUI.Create(this, session);
            blood = BloodBarUI.Create(session, rig.cam);
            minimap = MinimapUI.Create(session);   // 调试2-5：小地图（按 I 类矩阵、只显示去过的房间、点击放大）
            inventory = InventoryUI.Create(session);           // 调试3-4：背包界面（5 行 10 列 = 50 格）
            if (blood != null) blood.AttachInventory(inventory);
            started = true;
            Debug.Log($"[Game] 开始游戏：世界={session.WorldNameCn}(M={(int)session.currentKind}) 出生房间{session.playerRoom} 出生格{session.playerCell}");
        }

        void Update()
        {
            if (!started || session == null) return;

            // 8 方向移动：WASD / 方向键（两键斜向、三键按剩下的单键、四键互相抵消）
            Vector2Int dir = MoveInput.FromKeys(
                Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow),
                Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow),
                Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow),
                Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow));
            bool dash = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
            bool overMap = minimap != null && minimap.PointerInside;
            if (overMap && Input.GetMouseButtonDown(0)) minimap.ToggleZoom();   // 调试2-5：点小地图放大 / 还原（不挥剑）
            bool attack = Input.GetMouseButtonDown(0) && !overMap;
            bool jump = Input.GetKeyDown(KeyCode.Space);   // 文档 L31：空格发出跳跃指令（0.75s 升 0.75 格、0.75s 落回，空中仍可移动）
            // 调试2-2：鼠标滚轮缩放视角（上滑放大到最多 200%，下滑缩小到最少 50%）；缩放两种模式都生效
            if (rig != null && Input.mouseScrollDelta.y != 0f) rig.ZoomByScroll(Input.mouseScrollDelta.y);

            // 反馈 6：Ctrl+Shift+P 进入/退出调试模式（默认普通模式）。
            // 调试2-6：调试键与 Unity 快捷键冲突（Ctrl+Shift+P），改成 Alt+T
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool debugToggle = alt && Input.GetKeyDown(KeyCode.T);
            if (debugToggle)
            {
                session.SetDebugMode(!session.debugMode);
                if (hud != null) hud.SetDebugMode(session.debugMode);
                dash = false;      // 切换那一帧不要把按键当成移动/冲刺
            }

            if (Input.GetKeyDown(KeyCode.B) && inventory != null) inventory.Toggle();   // 调试3-4：B 键打开/关闭背包
            if (session.debugMode && Input.GetKeyDown(KeyCode.V)) session.ToggleWorldDebug();
            if (session.debugMode && Input.GetKeyDown(KeyCode.R)) session.ResetToSpawn();
            if (session.debugMode && Input.GetKeyDown(KeyCode.F1) && hud != null) hud.Toggle();

            session.Tick(Time.deltaTime, dir, dash, attack, jump);
        }
    }
}