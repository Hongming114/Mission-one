using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DungeonCity.Core;
using DungeonCity.Gen;
using DungeonCity.Game;
using DungeonCity.Sim;

namespace DungeonCity.UI
{
    /// <summary>HUD：主角血量/攻击、当前世界与房间、矩阵取值、刷怪判定与场上怪、最近事件（F1 开关）。</summary>
    public class HudUI : MonoBehaviour
    {
        GameBootstrap boot;
        GameSession session;
        Text body;
        Text title;
        float acc;
        bool visible = true;
        bool debugMode;

        /// <summary>反馈 6：默认普通模式＝HUD 与调试键都隐藏/失效；Alt+T 进入调试模式。</summary>
        public void SetDebugMode(bool on)
        {
            debugMode = on;
            visible = on;
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
            if (on) Refresh();
        }

        public static HudUI Create(GameBootstrap boot, GameSession session)
        {
            var go = new GameObject("HUD");
            var ui = go.AddComponent<HudUI>();
            ui.boot = boot;
            ui.session = session;
            ui.Build();
            ui.SetDebugMode(false);   // 反馈 6：默认进入时是普通模式（HUD 隐藏）
            return ui;
        }

        void Build()
        {
            Canvas canvas = UiFactory.CreateCanvas("HUD画布", transform, 50);
            Transform root = canvas.transform;

            RectTransform panel = UiFactory.Panel("面板", root, new Color(0.03f, 0.04f, 0.06f, 0.72f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -58f), new Vector2(760f, 560f));

            title = UiFactory.NewText("标题", panel, "地牢与城市", 26, TextAnchor.UpperLeft, new Color(0.95f, 0.96f, 1f));
            UiFactory.Place((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(0f, 34f));

            body = UiFactory.NewText("正文", panel, "", 21, TextAnchor.UpperLeft, new Color(0.82f, 0.86f, 0.94f));
            UiFactory.Stretch((RectTransform)body.transform, 18f, 12f, 18f, 52f);
            body.alignment = TextAnchor.UpperLeft;
        }

        public void Toggle()
        {
            if (!debugMode) return;   // 反馈 6：普通模式下 F1 不生效
            visible = !visible;
            gameObject.SetActive(visible);
        }

        void Update()
        {
            acc += Time.unscaledDeltaTime;
            if (acc < 0.2f) return;
            acc = 0f;
            Refresh();
        }

        public void Refresh()
        {
            if (session == null || body == null) return;
            WorldData w = session.World;
            PlayerSim pl = session.player;
            RoomData room = session.PlayerRoom;
            Vector2Int worldCell = session.PlayerWorldCell;
            Vector2Int localCell = session.PlayerLocalCell;

            string roomText = room == null
                ? "（在两房间之间的空隙里）"
                : $"{room.Key} {room.typeName} {room.width}x{room.height} 门[{DoorDirUtil.Describe(room.doors)}]";

            int far = 0, near = 0;
            foreach (KeyValuePair<Vector2Int, int> kv in session.PlayerRoomDistances())
            {
                if (kv.Value < 0) continue;
                if (kv.Value >= WorldCatalog.Dungeon().monsterMinRoomDistance) far++; else near++;
            }

            string judge = "还没判定过";
            if (session.lastJudge != null)
            {
                MonsterSpawnPlan p = session.lastJudge;
                judge = $"{p.MonsterCount} 只（抽 {p.sampledCells} 格 / 通过 {p.rolledSuccesses} 次 / 被上限压制 {p.suppressedByCap} 次）";
            }

            string state = pl.Dead ? "阵亡"
                : pl.Dashing ? "冲刺中（免疫伤害）"
                : pl.Attacking ? $"挥剑中 {pl.AttackProgress * 100f:0}%"
                : pl.hurtFlash > 0f ? "受击闪红"
                : "正常";

            float zoom = boot != null && boot.rig != null ? boot.rig.ZoomPercent : 100f;
            string nearest = "（当前不在同一世界 / 场上没有怪）";
            if (session.currentKind == WorldKind.Dungeon && session.slimes.Count > 0)
            {
                SlimeSim s0 = session.slimes[0];
                float d = Vector2.Distance(s0.pos, pl.pos);
                nearest = $"{s0.tag} HP {s0.hp}/{SlimeSim.HpDefault} 状态 {s0.state} 圆心距 {d:0.00} 格 间隙 {s0.AttackGapTo(pl):0.00} 格";
            }

            body.text =
                $"世界：{session.WorldNameCn}（M = {(int)session.currentKind}）　种子 {w.seed}　I 类矩阵 {w.iWidth} x {w.iHeight}　房间数 {w.RoomCount}\n" +
                $"当前房间：{roomText}\n" +
                $"玩家格：世界({worldCell.x},{worldCell.y})　局部({localCell.x},{localCell.y})　II = {session.TileAtPlayer}({TileSymbol.Of(session.TileAtPlayer)})　III = {FuncCode.Label(session.FuncAtPlayer)}\n" +
                $"朝向：{MoveInput.Describe(pl.facing)}（贴图 {MoveInput.SpriteIndex(pl.facing)}）　步数：{session.moveCount}　游玩 {session.playTime:0.0}s\n" +
                $"主角：HP {pl.hp}/{pl.hpMax}　ATK {pl.atk}　状态：{state}\n" +
                $"冲刺 {pl.dashCount} 次（冷却 {pl.DashCooldownLeft:0.0}s）　挥剑 {pl.swingCount} 次　命中 {session.hitCount} 次　被扑击 {pl.hurtCount} 次\n" +
                $"房间距离：满足刷怪距离(>= {WorldCatalog.Dungeon().monsterMinRoomDistance}) {far} 个　近处 {near} 个\n" +
                $"刷怪判定：第 {session.monsterDirector.judgeCount} 次　下次倒计时 {session.monsterDirector.TimeToNextJudge(session.slimes.Count):0.0}s（场上 <= 2 只时 5s 立即判定 / 否则 60s）\n" +
                $"最近一次判定：{judge}\n" +
                $"场上怪：{session.slimes.Count} / 上限 {session.MonsterCap}{(session.monsterDirector.Capped(session.slimes.Count, session.worlds[WorldKind.Dungeon]) ? "（已达上限：判定照做但不生成）" : "")}　击杀 {session.killCount} 只　重生 {session.respawnCount} 次\n" +
                $"相机缩放：{zoom:0}%（滚轮 上滑放大 / 下滑缩小，50%~200%）\n" +
                $"跳跃：{(pl.Jumping ? "浮空中" : "落地")}　高度 {pl.AirHeight:0.00} 格　跳跃 {pl.jumpCount} 次　{session.InventoryText}\n" +
                $"最近一只：{nearest}\n" +
                $"最近事件：{session.lastEvent}\n" +
                "操作：WASD/方向键 8 向移动（松手即停）　空格 跳跃（0.75s 升 0.75 格、0.75s 落回，空中可移动）　Shift 冲刺（0.5s / 约 2.25 格，带残影特效、途中免疫伤害）　左键 挥剑（逆时针 0.4s，扇形 1 格 / 150°）\n" +
                "　　　普通模式：没有调试界面/按键（Alt+T 进入调试模式；V 换世界 / R 回出生点 / F1 开关本面板）";
        }
    }
}
