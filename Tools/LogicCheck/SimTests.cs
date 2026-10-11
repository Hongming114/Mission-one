using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonCity.Core;
using DungeonCity.Gen;
using DungeonCity.Sim;

/// <summary>
/// 主角 / 史莱姆的玩法规则断言（纯 Sim 层，不需要 Unity）。由 Program.Main 调用。
/// </summary>
static class SimTests
{
    static Vector2 XZ(Vector3 v) { return new Vector2(v.x, v.z); }

    /// <summary>找一个"朝 dir 方向至少 needCells 格无阻挡"的房间内格（主角与史莱姆都能站在上面）。</summary>
    static bool FindOpenRun(WalkMap map, WorldData world, Vector2Int dir, int needCells, out Vector2 spot)
    {
        Vector2 dv = new Vector2(dir.x, dir.y);
        foreach (Vector2Int key in WorldGenerator.SortedKeys(world))
        {
            RoomData r = world.Room(key);
            if (r == null) continue;
            for (int x = 1; x <= r.width - 2; x++)
                for (int y = 1; y <= r.height - 2; y++)
                {
                    if (!FuncCode.CanStand(r.Func(x, y))) continue;
                    Vector2 c = XZ(r.CellCenter(x, y));
                    if (map.Blocked(c, 0.45f, true) || map.Blocked(c, 0.45f, false)) continue;
                    bool ok = true;
                    for (int k = 1; k <= needCells + 1 && ok; k++)
                    {
                        Vector2 p = c + dv * k;
                        if (map.Blocked(p, 0.45f, true) || map.Blocked(p, 0.45f, false)) ok = false;
                    }
                    if (ok) { spot = c; return true; }
                }
        }
        spot = Vector2.zero;
        return false;
    }

    /// <summary>找第一个 III 类矩阵等于 func 的世界格。</summary>
    static Vector2Int FindFuncCell(WorldData world, float func)
    {
        foreach (Vector2Int key in WorldGenerator.SortedKeys(world))
        {
            RoomData r = world.Room(key);
            if (r == null) continue;
            for (int x = 0; x < r.width; x++)
                for (int y = 0; y < r.height; y++)
                    if (Mathf.Abs(r.Func(x, y) - func) < 1e-4f) return WalkMap.CellOf(XZ(r.CellCenter(x, y)));
        }
        return new Vector2Int(int.MinValue, int.MinValue);
    }

    public static void Run(WorldData city, WorldData dungeon, Action<bool, string> check, Action<string> echo)
    {
        echo("");
        echo("=== 主角 / 史莱姆 玩法规则断言（Sim 层：输入、碰撞、穿门、冲刺、挥剑、史莱姆 AI）===");

        // ---------------- 1. 输入 -> 8 方向 ----------------
        check(MoveInput.FromKeys(true, false, false, false) == new Vector2Int(0, 1), "W → 上（0,1）");
        check(MoveInput.FromKeys(false, false, false, true) == new Vector2Int(1, 0), "D → 右（1,0）");
        check(MoveInput.FromKeys(true, false, true, false) == new Vector2Int(-1, 1), "W+A → 左上（两键 = 斜向）");
        check(MoveInput.FromKeys(true, false, true, true) == new Vector2Int(0, 1), "W+A+D → 上（左右互相抵消，三键时按剩下的那个单键走）");
        check(MoveInput.IsNone(MoveInput.FromKeys(true, true, true, true)), "W+S+A+D → 不动（四键互相抵消）");

        Vector2Int[] dirs = { new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1),
                              new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(1, -1) };
        bool spriteOk = true;
        for (int i = 0; i < 8; i++) if (MoveInput.SpriteIndex(dirs[i]) != i) spriteOk = false;
        check(spriteOk, "8 个方向对应 8 张差分贴图（0下 1左下 2左 3左上 4上 5右上 6右 7右下）");
        check(Mathf.Abs(MoveInput.ToVector(new Vector2Int(-1, -1)).magnitude - 1f) < 1e-4f, "斜向速度归一化（不比直走快）");
        check(MoveInput.AxisDir(new Vector2(0.9f, 0.1f)) == new Vector2Int(1, 0) && MoveInput.AxisDir(new Vector2(0.1f, 0.1f)) == new Vector2Int(0, 0),
              "朝向阈值 0.15：0.9 → 1，0.1 → 0");

        // ---------------- 2. 关卡换算与可走性 ----------------
        WalkMap map = new WalkMap(dungeon, 18f);
        RoomData spawnRoom = dungeon.SpawnRoom;
        Vector2 spawnPos = XZ(spawnRoom.CellCenter(dungeon.spawnCell.x, dungeon.spawnCell.y));
        RoomData backRoom; Vector2Int backLocal;
        check(map.TryRoomOf(spawnPos, out backRoom, out backLocal) && backRoom.grid == spawnRoom.grid,
              "WalkMap 能把世界坐标一步换算回「房间 + 房内格」");
        check(map.IsWalkable(WalkMap.CellOf(spawnPos), true), "出生格对主角可走");
        check(!map.IsWalkable(new Vector2Int(9999, 9999), true) && map.Blocked(WalkMap.CenterOf(new Vector2Int(9999, 9999)), 0.3f, true),
              "房间之间的空隙 = 墙（既不可走、也会挡住体积）");

        Vector2Int doorCell = FindFuncCell(dungeon, FuncCode.RoomPortal);
        Vector2Int ladderCell = FindFuncCell(dungeon, FuncCode.WorldPortal);
        check(doorCell.x != int.MinValue && map.IsWalkable(doorCell, true), "门格 III=2.1 可走（能走过去换房间）");
        check(ladderCell.x != int.MinValue && map.IsWalkable(ladderCell, true) && !map.IsWalkable(ladderCell, false),
              "梯子格 III=2.2：主角可走，史莱姆当墙（不跟去另一个世界）");

        // ---------------- 3. 穿门：换成配对房间，且不会卡在门格里 ----------------
        RoomData doorRoom; Vector2Int doorLocal;
        bool doorRoomOk = map.TryRoomOf(WalkMap.CenterOf(doorCell), out doorRoom, out doorLocal);
        PlayerSim walker = new PlayerSim();
        walker.pos = WalkMap.CenterOf(doorCell);
        bool portaled = walker.ExitPortalIfAny(map, true);
        RoomData afterRoom; Vector2Int afterLocal;
        bool afterRoomOk = map.TryRoomOf(walker.pos, out afterRoom, out afterLocal);
        check(doorRoomOk && portaled && afterRoomOk, "踩上门格会触发穿门（ExitPortalIfAny 返回 true）");
        check(portaled && afterRoomOk && afterRoom.grid != doorRoom.grid, $"穿门后换到配对房间（{doorRoom.Key} → {afterRoom.Key}）");
        check(portaled && !FuncCode.IsRoomPortal(map.FuncAt(WalkMap.CellOf(walker.pos))) && map.IsWalkable(WalkMap.CellOf(walker.pos), true),
              "落点在配对门的内侧一格（不会刚落地又踩在门格里");
        check(!walker.ExitPortalIfAny(map, true) && walker.portalCooldown > 0f, "穿门后有一小段冷却（不会来回传送）");

        // ---------------- 4. 移动速度与冲刺 ----------------
        Vector2 spot;
        bool foundSpot = FindOpenRun(map, dungeon, new Vector2Int(1, 0), 3, out spot);
        check(foundSpot, "地牢里找到一块向东至少 3 格无阻挡的空地（移动测试场地）");

        PlayerSim walk = new PlayerSim();
        walk.pos = spot;
        Vector2Int east = new Vector2Int(1, 0);   // 反馈 2：现在必须每帧给方向，松手（None）就停
        walk.facing = new Vector2Int(1, 0);
        Vector2 walkStart = walk.pos;
        for (int i = 0; i < 100; i++) walk.Tick(0.01f, map, east, false, false);
        float plain = Vector2.Distance(walkStart, walk.pos);
        // 调试2-3：走速 2 -> 3 格/秒（冲刺倍率同时 1.25 -> 1.5，见 PlayerSim）
        check(Mathf.Abs(plain - 3f) <= 0.08f, $"正常移动 3 格/秒（1 秒实测 {plain:0.00} 格）");

        echo("=== 相机缩放与视角（调试2-1/2/4）===");
        check(Mathf.Abs(DungeonCity.Core.CameraZoom.Clamp(10f) - DungeonCity.Core.CameraZoom.MinPercent) < 0.001f, "滚轮缩放下限 = 50%（把 10% 夹到 50%）");
        check(Mathf.Abs(DungeonCity.Core.CameraZoom.Clamp(500f) - DungeonCity.Core.CameraZoom.MaxPercent) < 0.001f, "滚轮缩放上限 = 200%（把 500% 夹到 200%）");
        check(Mathf.Abs(DungeonCity.Core.CameraZoom.ApplyScroll(100f, 1f) - 125f) < 0.001f, "滚轮上滑一格：100% -> 125%（放大）");
        check(Mathf.Abs(DungeonCity.Core.CameraZoom.ApplyScroll(100f, -1f) - 75f) < 0.001f, "滚轮下滑一格：100% -> 75%（缩小）");
        check(Mathf.Abs(DungeonCity.Core.CameraZoom.ApplyScroll(200f, 1f) - 200f) < 0.001f, "已在上限继续上滑仍是 200%");
        check(Mathf.Abs(DungeonCity.Core.CameraZoom.ApplyScroll(50f, -1f) - 50f) < 0.001f, "已在下限继续下滑仍是 50%");
        float baseSize = 11f / 1.75f;
        check(Mathf.Abs(DungeonCity.Core.CameraZoom.OrthoSize(baseSize, 100f) - baseSize) < 0.01f, $"100% 基准 = 调试1 的 175% 视角（正交尺寸 {baseSize:0.000}）");
        check(Mathf.Abs(DungeonCity.Core.CameraZoom.OrthoSize(baseSize, 200f) - baseSize / 2f) < 0.01f, "200% 时正交尺寸减半（视野更大）");
        check(Mathf.Abs(DungeonCity.Core.CameraZoom.OrthoSize(baseSize, 50f) - baseSize * 2f) < 0.01f, "50% 时正交尺寸翻倍（视野更小）");

        // 反馈 2：松开方向键后主角不再继续移动（旧实现会一直走）
        Vector2 beforeRelease = walk.pos;
        for (int i = 0; i < 100; i++) walk.Tick(0.01f, map, MoveInput.None, false, false);
        check(Vector2.Distance(beforeRelease, walk.pos) < 0.01f, $"松开方向键立即停下（1 秒内位移 {Vector2.Distance(beforeRelease, walk.pos):0.000} 格）");
        check(walk.facing == east, "停下来后仍保持原来的朝向（贴图不回到默认）");

        PlayerSim dasher = new PlayerSim();
        dasher.pos = spot;
        dasher.inputDir = new Vector2Int(1, 0);
        dasher.facing = new Vector2Int(1, 0);
        Vector2 dashStart = dasher.pos;
        dasher.Tick(0.01f, map, MoveInput.None, true, false);
        check(dasher.Dashing && dasher.Invulnerable, "冲刺中：Dashing = true 且处于免疫伤害状态");
        int guard = 0;
        while (dasher.Dashing && guard++ < 200) dasher.Tick(0.01f, map, MoveInput.None, false, false);
        float dash = Vector2.Distance(dashStart, dasher.pos);
        float dashWant = PlayerSim.DashDistance * PlayerSim.DashSpeedMultiplier;
        check(Mathf.Abs(dash - dashWant) <= 0.06f, $"反馈 7：冲刺 0.5 秒位移约 {dashWant:0.00} 格（实测 {dash:0.00} 格）");
        check(!dasher.CanDash && dasher.DashCooldownLeft > 0.6f, $"冲刺结束后进入冷却，不能立刻再冲（剩余 {dasher.DashCooldownLeft:0.00}s）");

        PlayerSim dodger = new PlayerSim();
        dodger.pos = spot;
        dodger.Tick(0.01f, map, MoveInput.None, true, false);
        int hpBefore = dodger.hp;
        bool absorbed = !dodger.Hurt(5, "测试史莱姆");
        check(absorbed && dodger.hp == hpBefore, "冲刺过程中免疫伤害（Hurt 返回 false、HP 不变）");

        // 自我审查第 1 条：冲刺撞墙会立即结束
        PlayerSim bumper = new PlayerSim();
        bool foundWall = false;
        Vector2 wallSpot = Vector2.zero;
        foreach (Vector2Int key in WorldGenerator.SortedKeys(dungeon))
        {
            RoomData rr = dungeon.Room(key);
            if (rr == null) continue;
            for (int y = 1; y <= rr.height - 2 && !foundWall; y++)
            {
                if (!rr.CanStand(1, y)) continue;
                Vector2 c = XZ(rr.CellCenter(1, y));
                if (!map.Blocked(c, 0.3f, true)) { wallSpot = c; foundWall = true; }
            }
            if (foundWall) break;
        }
        check(foundWall, "找到一块贴着西墙的空地（冲刺撞墙测试场地）");
        if (foundWall)
        {
            bumper.pos = wallSpot;
            bumper.facing = new Vector2Int(-1, 0);
            bumper.Tick(0.01f, map, MoveInput.None, true, false);
            guard = 0;
            while (bumper.Dashing && guard++ < 200) bumper.Tick(0.01f, map, MoveInput.None, false, false);
            check(!bumper.Dashing && bumper.dashBlockedCount > 0, "冲刺撞到墙会提前结束（dashBlockedCount 累加）");
        }

        // 自我审查第 2 条：受伤后 0.6 秒无敌帧
        PlayerSim tough = new PlayerSim();
        tough.pos = spot;
        bool firstHurt = tough.Hurt(5, "测试");
        int toughHp = tough.hp;
        bool secondHurt = tough.Hurt(5, "测试");
        check(firstHurt && toughHp == PlayerSim.MaxHpDefault - 5 && !secondHurt && tough.hp == toughHp,
              $"受伤后 0.6 秒无敌帧：连续第二次 Hurt 被挡下（HP 仍是 {tough.hp}）");
        for (int i = 0; i < 70; i++) tough.Tick(0.01f, map, MoveInput.None, false, false);
        check(tough.Hurt(5, "测试") && tough.hp == toughHp - 5, $"无敌帧结束后可以再次受伤（{toughHp} → {tough.hp}）");

        // 自我审查第 3 条：隔着墙不能互殴
        check(LineOfSight.Clear(map, spot, spot + new Vector2(1f, 0f), true), "同一房间里视线畅通（LineOfSight.Clear = true）");
        check(!LineOfSight.Clear(map, spot, spot + new Vector2(0f, -20f), true), "穿过房间墙壁/空隙的视线被挡（Clear = false）");

        PlayerSim plainHurt = new PlayerSim();
        plainHurt.pos = spot;
        check(plainHurt.Hurt(5, "测试史莱姆") && plainHurt.hp == PlayerSim.MaxHpDefault - 5 && plainHurt.hurtFlash > 0f,
              $"非冲刺状态会受伤：100 → {plainHurt.hp}（并触发受击闪红）");

        // ---------------- 5. 挥剑：扇形 1 格 / 圆心角 150°，逆时针扫 ----------------
        PlayerSim hero = new PlayerSim();
        hero.pos = spot;
        hero.inputDir = MoveInput.None;
        hero.facing = new Vector2Int(1, 0);
        SlimeSim front = new SlimeSim(1);
        front.pos = spot + new Vector2(0.7f, 0f);
        SlimeSim behind = new SlimeSim(2);
        behind.pos = spot + new Vector2(-0.7f, 0f);
        check(CombatSector.Contains(hero.pos, hero.facing, PlayerSim.SwordLength, PlayerSim.SwordHalfAngleDeg, front.pos, front.radius),
              "扇形判定：正前方 0.7 格在剑的范围内");
        check(!CombatSector.Contains(hero.pos, hero.facing, PlayerSim.SwordLength, PlayerSim.SwordHalfAngleDeg, behind.pos, behind.radius),
              "扇形判定：正后方 0.7 格不在范围内（圆心角 150°）");

        front.state = SlimeState.Charge;            // 让史莱姆先处在攻击动作里
        front.stateTimer = SlimeSim.ChargeSeconds;
        hero.Tick(0.01f, map, MoveInput.None, false, true);
        check(hero.Attacking && hero.BladeRelativeDeg <= -80f, "挥剑起手：剑刃从 -90° 开始（逆时针扫 180°）");
        int frontHits = 0, backHits = 0;
        guard = 0;
        while (hero.Attacking && guard++ < 300)
        {
            hero.Tick(0.01f, map, MoveInput.None, false, false);
            float relF = CombatSector.RelativeDeg(hero.facing, hero.pos, front.pos);
            float relB = CombatSector.RelativeDeg(hero.facing, hero.pos, behind.pos);
            if (CombatSector.Contains(hero.pos, hero.facing, PlayerSim.SwordLength, PlayerSim.SwordHalfAngleDeg, front.pos, front.radius) && hero.Swept(relF, relF))
            { front.TakeHit(hero.atk, front.pos - hero.pos); frontHits++; }
            if (CombatSector.Contains(hero.pos, hero.facing, PlayerSim.SwordLength, PlayerSim.SwordHalfAngleDeg, behind.pos, behind.radius) && hero.Swept(relB, relB))
            { behind.TakeHit(hero.atk, behind.pos - hero.pos); backHits++; }
        }
        check(frontHits == 1 && front.hp == SlimeSim.HpDefault - PlayerSim.AtkDefault,
              $"一次挥剑只命中一次：前方史莱姆 20 → {front.hp}（命中 {frontHits} 帧）");
        check(backHits == 0 && behind.hp == SlimeSim.HpDefault, "背后的史莱姆不掉血（挥剑有方向）");
        check(front.InHurt && !front.Attacking, "受击后立即终止自身攻击动作（蓄力/扑击被打断）并变红");

        // ---------------- 6. 受击：击退 0.5 格 ----------------
        SlimeSim knocked = new SlimeSim(3);
        Vector2 knockedStart = spot + new Vector2(0.7f, 0f);
        knocked.pos = knockedStart;
        knocked.TakeHit(PlayerSim.AtkDefault, knocked.pos - spot);      // 从主角那边打过来 → 沿 +x 击退
        check(knocked.hurtFlash > 0f && knocked.InHurt, "受击：进入受击状态（变红计时 > 0）");
        guard = 0;
        while (knocked.InHurt && guard++ < 80) knocked.Tick(0.01f, map, dungeon, hero);
        float knock = Vector2.Distance(knockedStart, knocked.pos);
        check(Mathf.Abs(knock - SlimeSim.KnockbackDistance) <= 0.10f, $"受击被击退 0.5 格（实测 {knock:0.00} 格）");
        check(knocked.pos.x > knockedStart.x, "击退方向 = 远离主角的方向");

        // ---------------- 7. 史莱姆：视野 / 蓄力 / 扑击 / 伤害 ----------------
        PlayerSim victim = new PlayerSim();
        victim.pos = spot;
        victim.inputDir = MoveInput.None;
        victim.facing = new Vector2Int(-1, 0);
        SlimeSim attacker = new SlimeSim(4);
        attacker.pos = spot + new Vector2(0.9f, 0f);      // 圆心距 0.9 → 间隙 0.2 ≤ 0.5
        check(attacker.AttackGapTo(victim) <= SlimeSim.AttackGap && attacker.Sees(victim),
              $"圆心距 0.9 格时间隙 {attacker.AttackGapTo(victim):0.00} 格（半径 0.3+0.4，贴住即攻击）");
        bool sawCharge = false, sawLunge = false;
        float clock = 0f, firstHitAt = -1f;
        for (int i = 0; i < 400; i++)
        {
            attacker.Tick(0.01f, map, dungeon, victim);
            if (attacker.state == SlimeState.Charge) sawCharge = true;
            if (attacker.state == SlimeState.Lunge)
            {
                sawLunge = true;
                if (!attacker.lungeHit && attacker.AttackGapTo(victim) <= 0.12f)
                {
                    attacker.lungeHit = true;
                    attacker.hitPlayerCount++;
                    victim.Hurt(attacker.atk, attacker.tag);
                    if (firstHitAt < 0f) firstHitAt = clock;
                }
            }
            if (attacker.state != SlimeState.Lunge) attacker.lungeHit = false;
            clock += 0.01f;
            if (victim.hp < PlayerSim.MaxHpDefault) break;
        }
        check(sawCharge, "间隙 ≤ 0.5 格时史莱姆进入蓄力（Charge）");
        check(sawLunge && firstHitAt >= 0.95f && firstHitAt <= 1.7f, $"蓄力 1 秒后扑击并命中（实测第 {firstHitAt:0.00} 秒）");
        check(victim.hp == PlayerSim.MaxHpDefault - SlimeSim.AtkDefault, $"史莱姆攻击伤害 5：主角 100 → {victim.hp}");

        PlayerSim far = new PlayerSim();
        far.pos = spot;
        SlimeSim blind = new SlimeSim(5);
        blind.pos = spot + new Vector2(6f, 0f);          // 视野半径 3 格之外（文档 L37 对齐）
        Vector2 blindStart = blind.pos;
        for (int i = 0; i < 100; i++) blind.Tick(0.01f, map, dungeon, far);
        check(!blind.Sees(far), "视野半径 3 格：6 格之外看不见主角");
        // 反馈 5：追人闸门 = 视野内（3 格 + 视线）或同一个房间，且房间图距离 ≤ 2；相隔 ≥3 个房间的怪只会自己游荡
        Dictionary<Vector2Int, int> gateDist = RoomDistanceField.Distances(dungeon, dungeon.SpawnRoom.grid);
        Vector2Int gateKey = new Vector2Int(int.MinValue, int.MinValue);
        foreach (Vector2Int key in WorldGenerator.SortedKeys(dungeon))
        {
            int gd;
            if (gateDist.TryGetValue(key, out gd) && gd >= 3) { gateKey = key; break; }
        }
        if (gateKey.x != int.MinValue)
        {
            RoomData gr = dungeon.Room(gateKey);
            Vector2Int gc = new Vector2Int(int.MinValue, int.MinValue);
            for (int x = 1; x <= gr.width - 2 && gc.x == int.MinValue; x++)
                for (int y = 1; y <= gr.height - 2; y++) if (gr.CanStand(x, y)) { gc = new Vector2Int(x, y); break; }
            if (gc.x != int.MinValue)
            {
                SlimeSim distant = new SlimeSim(15);
                distant.pos = XZ(gr.CellCenter(gc.x, gc.y));
                bool everChased = false;
                for (int i = 0; i < 200; i++)
                {
                    distant.Tick(0.01f, map, dungeon, far);
                    if (distant.state == SlimeState.Chase || distant.state == SlimeState.Charge) everChased = true;
                }
                check(!everChased && !distant.Sees(far), $"相隔 {gateKey} 的史莱姆不会追主角（视野 + 同房间 + 房间距离三重闸门）");
            }
        }

        // ---------------- 8. 碰撞体积与击杀 ----------------
        PlayerSim a = new PlayerSim();
        a.pos = spot;
        SlimeSim b = new SlimeSim(6);
        b.pos = spot + new Vector2(0.1f, 0f);
        ActorBody.Separate(a, b, map, true, false);
        float sepDist = Vector2.Distance(a.pos, b.pos);
        check(sepDist >= a.radius + b.radius - 0.02f, $"主角与史莱姆不会重合（推开后圆心距 {sepDist:0.00} 格，半径和 {a.radius + b.radius:0.00}）");

        SlimeSim doomed = new SlimeSim(7);
        bool lethal1 = doomed.TakeHit(PlayerSim.AtkDefault, new Vector2(1f, 0f));
        int hpAfterFirst = doomed.hp;
        bool lethal2 = doomed.TakeHit(PlayerSim.AtkDefault, new Vector2(1f, 0f));
        check(!lethal1 && hpAfterFirst == SlimeSim.HpDefault - PlayerSim.AtkDefault, $"第一剑：20 → {hpAfterFirst}（ATK 10）");
        check(lethal2 && doomed.Dead, "第二剑：hp ≤ 0 → 判定为击杀（返回 true、Dead）");

        // ---------------- 9. 反馈 5：跨房间追踪 / 自主游荡 / 不坐梯子 ----------------
        SlimeSim tracker = new SlimeSim(11);
        tracker.pos = spot;
        PlayerSim seen = new PlayerSim();
        seen.pos = spot + new Vector2(1f, 0f);
        tracker.Tick(0.01f, map, dungeon, seen);
        check(tracker.Tracking, "看见主角后写入记忆（Tracking = true）");
        Vector2 memStart = tracker.pos;
        for (int i = 0; i < 100; i++) tracker.Tick(0.01f, map, dungeon, null, false);
        check(Vector2.Distance(memStart, tracker.pos) > 0.05f && !tracker.Dead,
              $"主角离开这个世界后，史莱姆仍朝记忆位置移动（{Vector2.Distance(memStart, tracker.pos):0.00} 格）");

        SlimeSim wander = new SlimeSim(12);
        wander.pos = spot;
        Vector2 wStart = wander.pos;
        float wMax = 0f;
        for (int i = 0; i < 1200; i++) { wander.Tick(0.01f, map, dungeon, null, false); wMax = Mathf.Max(wMax, Vector2.Distance(wStart, wander.pos)); }
        check(wander.roamCount > 0 && wMax > 0.3f, $"没有主角时史莱姆会自己游荡（选点 {wander.roamCount} 次，最远走了 {wMax:0.00} 格）");

        SlimeSim noLadder = new SlimeSim(13);
        noLadder.pos = spot;
        check(Vector2.Distance(noLadder.ClampTarget(map, WalkMap.CenterOf(ladderCell)), WalkMap.CenterOf(ladderCell)) > 0.01f,
              "ClampTarget 把梯子格目标换成同房间最近的可行走格（绝不坐梯子）");
        noLadder.SetMemoryTo(map, dungeon, ladderCell);
        for (int i = 0; i < 900; i++) noLadder.Tick(0.01f, map, dungeon, null, false);
        check(!FuncCode.IsWorldPortal(map.FuncAt(WalkMap.CellOf(noLadder.pos))), "史莱姆绝不会站到梯子格上（不坐梯子跨世界）");
        check(noLadder.follow.avoidWorldPortals, "史莱姆的寻路会跳过梯子（follow.avoidWorldPortals = true）");

        // 自我审查第 4 条：房间图距离 > 2 的怪不会追过来
        Dictionary<Vector2Int, int> farDist = RoomDistanceField.Distances(dungeon, dungeon.worldPortalRoom);
        Vector2Int farRoomKey = new Vector2Int(int.MinValue, int.MinValue);
        foreach (Vector2Int key in WorldGenerator.SortedKeys(dungeon))
        {
            int d;
            if (farDist.TryGetValue(key, out d) && d > SlimeSim.ChaseRoomRange) { farRoomKey = key; break; }
        }
        if (farRoomKey.x != int.MinValue)
        {
            RoomData fr = dungeon.Room(farRoomKey);
            Vector2Int fc = new Vector2Int(int.MinValue, int.MinValue);
            for (int x = 1; x <= fr.width - 2 && fc.x == int.MinValue; x++)
                for (int y = 1; y <= fr.height - 2; y++) if (fr.CanStand(x, y)) { fc = new Vector2Int(x, y); break; }
            if (fc.x != int.MinValue)
            {
                SlimeSim farSlime = new SlimeSim(14);
                farSlime.pos = XZ(fr.CellCenter(fc.x, fc.y));
                farSlime.SetMemoryTo(map, dungeon, ladderCell);
                check(!farSlime.hasMemory, $"房间距离 > {SlimeSim.ChaseRoomRange} 的史莱姆不会追（避免全图一起冲）");
            }
        }

        // ---------------- 10. 文档第三次增量：跳跃 / 背包 / 史莱姆清 ----------------
        PlayerSim jumper = new PlayerSim();
        jumper.pos = spot;
        jumper.Tick(0.01f, map, MoveInput.None, false, false, true);
        check(jumper.Jumping && jumper.jumpCount == 1, "空格发出跳跃指令 -> 进入浮空（Jumping = true）");
        float peak = 0f, timeAtPeak = 0f, tAcc = 0f;
        for (int i = 0; i < 151; i++)
        {
            jumper.Tick(0.01f, map, MoveInput.None, false, false);
            tAcc += 0.01f;
            if (jumper.AirHeight > peak) { peak = jumper.AirHeight; timeAtPeak = tAcc; }
        }
        check(Mathf.Abs(peak - PlayerSim.JumpHeight) < 0.06f, $"跳跃最高 0.75 格（调试3-2：高度改为 50%，实测 {peak:0.00} 格，用时 {timeAtPeak:0.00}s ≈ 0.75s）");
        check(!jumper.Jumping && jumper.AirHeight < 1e-4f, $"起跳后 1.5s 落回地面（浮空结束，高度 {jumper.AirHeight:0.000}）");

        PlayerSim airMover = new PlayerSim();
        airMover.pos = spot;
        airMover.Tick(0.01f, map, MoveInput.None, false, false, true);
        Vector2 airStart = airMover.pos;
        for (int i = 0; i < 20; i++) airMover.Tick(0.01f, map, east, false, false);
        check(airMover.Jumping && Vector2.Distance(airStart, airMover.pos) > 0.4f,
              $"跳跃与行走是分运动：空中 0.2s 仍水平移动 {Vector2.Distance(airStart, airMover.pos):0.00} 格（合运动）");

        Inventory bag = new Inventory();
        int put1 = bag.Add(SlimeDrop.ItemName, 1);
        check(put1 == 1 && bag.SlotNumberOf(SlimeDrop.ItemName) == 1, $"第一种物品进 1 号格子（{SlimeDrop.ItemName} -> {bag.SlotNumberOf(SlimeDrop.ItemName)} 号格）");
        bag.Add(SlimeDrop.ItemName, 5);
        check(bag.Count(SlimeDrop.ItemName) == 6 && bag.UsedSlots == 1, $"同种物品放同一格（{SlimeDrop.ItemName} 共 {bag.Count(SlimeDrop.ItemName)} 个，占用 {bag.UsedSlots} 格）");
        int put2 = bag.Add("测试物品", 3);
        check(put2 == 3 && bag.SlotNumberOf("测试物品") == 2, "第二种物品进 2 号格子（按第一次获得的次序编号）");

        Inventory capped = new Inventory();
        int putCap = capped.Add("A", 200);
        check(putCap == Inventory.StackMax && capped.SlotAmount(1) == Inventory.StackMax, $"单格上限 99（存 200 实存 {putCap}）");
        check(capped.Add("A", 1) == 0, "该格已到 99：存放指令返回 0（外层据此终止存放）");

        Inventory full = new Inventory();
        for (int i = 0; i < Inventory.SlotCount; i++) full.Add("物品" + i, 1);
        int put51 = full.Add("第 51 种物品", 1);
        check(full.UsedSlots == Inventory.SlotCount && full.TypeFull && put51 == 0 && !full.Has("第 51 种物品"),
              $"50 格放满后不再存入新物品类型（已用 {full.UsedSlots}/{Inventory.SlotCount}，第 51 种返回 {put51} -> 掉落物留在原地）");

        check(Mathf.Abs(SlimeDrop.SizeCells - 0.6f) < 1e-4f && Mathf.Abs(SlimeDrop.PickupDistance - (0.3f + SlimeDrop.Radius)) < 1e-4f,
              $"史莱姆清 0.6×0.6 格：碰撞拾取圆心距 {SlimeDrop.PickupDistance:0.00} = 主角 0.30 + 掉落 {SlimeDrop.Radius:0.00}");

        check(Mathf.Abs(PlayerSim.AttackDuration - 0.4f) < 1e-4f, $"文档 L30 对齐：挥剑 {PlayerSim.AttackDuration:0.00}s（原 0.5s）");
        check(Mathf.Abs(SlimeSim.VisionRadius - 3f) < 1e-4f, $"文档 L37 对齐：史莱姆视野 {SlimeSim.VisionRadius:0.0f} 格（原 4 格）");

        // ---------------- 11. 调试3：跳跃高度 50% / 同房间禁止跨房间移动 ----------------
        echo("=== 11. 调试3：跳跃高度与跨房间规则 ===");
        check(Mathf.Abs(PlayerSim.JumpHeight - 0.75f) < 1e-4f,
              $"调试3-2：跳跃高度改为 50% -> {PlayerSim.JumpHeight:0.00} 格（原 1.5 格）");

        {
            WalkMap d3map = new WalkMap(dungeon, 18f);
            Vector2Int d3homeKey = dungeon.SpawnRoom.grid;
            RoomData d3home = dungeon.Room(d3homeKey);
            Vector2 d3homeSpot = spot;
            bool d3homeFound = false;
            for (int y = 1; y < d3home.height - 1 && !d3homeFound; y++)
            {
                for (int x = 1; x < d3home.width - 1; x++)
                {
                    if (!d3home.CanStand(x, y)) continue;
                    d3homeSpot = XZ(d3home.CellCenter(x, y));
                    d3homeFound = true;
                    break;
                }
            }

            SlimeSim d3pinned = new SlimeSim(21);
            d3pinned.pos = d3homeSpot;
            PlayerSim d3buddy = new PlayerSim();
            d3buddy.pos = d3homeSpot;
            for (int i = 0; i < 120; i++) d3pinned.Tick(1f / 60f, d3map, dungeon, d3buddy, true);
            check(d3pinned.pinnedToRoom, "调试3-1：主角与怪同房间时 pinnedToRoom = true（不允许任何跨房间移动）");

            for (int i = 0; i < 20 * 60; i++) d3pinned.Tick(1f / 60f, d3map, dungeon, d3buddy, true);
            RoomData d3stayRoom;
            Vector2Int d3stayLocal;
            bool d3stayInside = d3map.TryRoomOf(d3pinned.pos, out d3stayRoom, out d3stayLocal);
            check(d3stayInside && d3stayRoom.grid == d3homeKey,
                  $"调试3-1：同房间 20 秒内怪始终留在本房间（现在 {(d3stayInside ? d3stayRoom.grid.ToString() : "房间之外")}）");

            Vector2Int d3otherKey = d3homeKey;
            List<Vector2Int> d3roamKeys = WorldGenerator.SortedKeys(dungeon);
            for (int i = 0; i < d3roamKeys.Count; i++) { if (d3roamKeys[i] != d3homeKey) { d3otherKey = d3roamKeys[i]; break; } }
            RoomData d3other = dungeon.Room(d3otherKey);
            Vector2 d3otherSpot = d3homeSpot;
            bool d3otherFound = false;
            for (int y = 1; y < d3other.height - 1 && !d3otherFound; y++)
            {
                for (int x = 1; x < d3other.width - 1; x++)
                {
                    if (!d3other.CanStand(x, y)) continue;
                    d3otherSpot = XZ(d3other.CellCenter(x, y));
                    d3otherFound = true;
                    break;
                }
            }
            SlimeSim d3loose = new SlimeSim(22);
            d3loose.pos = d3homeSpot;
            PlayerSim d3mate = new PlayerSim();
            d3mate.pos = d3otherSpot;
            for (int i = 0; i < 120; i++) d3loose.Tick(1f / 60f, d3map, dungeon, d3mate, true);
            check(!d3loose.pinnedToRoom,
                  $"调试3-1：主角在别的房间 {d3homeKey} -> {d3otherKey} 时不禁止跨房间（怪可以走到门前一格再传送）");

            SlimeSim d3alone = new SlimeSim(23);
            d3alone.pos = d3homeSpot;
            for (int i = 0; i < 60; i++) d3alone.Tick(1f / 60f, d3map, dungeon, null, false);
            check(!d3alone.pinnedToRoom, "调试3-1：主角不在本世界时不限制跨房间");
        }


            // ---------------- 12. 调试4：遮挡（顶点高度剪切） ----------------
            echo("=== 12. 调试4 遮挡：有高度的顶点沿视线压平（屏幕不变、深度只看水平位置） ===");
            {
                Vector3[] d4pts = new Vector3[]
                {
                    new Vector3(0f, 0.5f, 3f),
                    new Vector3(2f, 1.4f, -1f),
                    new Vector3(-1f, 3f, 0.5f)
                };
                bool d4screenOk = true, d4depthOk = true;
                for (int i = 0; i < d4pts.Length; i++)
                {
                    Vector3 f = ViewShear.Flatten(d4pts[i]);
                    if (Math.Abs(ViewShear.ScreenUp(d4pts[i]) - ViewShear.ScreenUp(f)) > 0.0001f) d4screenOk = false;
                    float want = Mathf.Cos(ViewShear.PitchDeg * Mathf.Deg2Rad) * d4pts[i].z;
                    if (Math.Abs(ViewShear.Depth(f) - want) > 0.0001f) d4depthOk = false;
                }
                check(d4screenOk, "调试4：压平后屏幕位置不变（画面与压平前完全一致）");
                check(d4depthOk, "调试4：压平后深度只与水平位置有关（= cos(pitch) * z）");

                float d4player = ViewShear.Depth(new Vector3(0f, 0f, 0f));
                float d4south = ViewShear.Depth(ViewShear.Flatten(new Vector3(0f, 1.4f, -1.5f)));
                float d4north = ViewShear.Depth(ViewShear.Flatten(new Vector3(0f, 1.4f, 0.5f)));
                float d4topSameGround = Math.Abs(ViewShear.Depth(ViewShear.Flatten(new Vector3(0f, 1.4f, 0.5f))) - ViewShear.Depth(new Vector3(0f, 0f, 0.5f)));
                check(d4south < d4player, "调试4：南侧（相机与玩家之间）的墙顶面在玩家之前 -> 允许遮住玩家下半身");
                check(d4north > d4player, "调试4：北侧（玩家身后）的墙顶面在玩家之后 -> 不会在角色身上切横线");
                check(d4topSameGround < 0.0001f, "调试4：墙顶面深度与同格地面相同（高度不再参与遮挡 -> 横线消失）");
            }
        echo($"  （史莱姆 0.8×0.8 格：半径 {b.radius:0.00} 格；主角半径 {a.radius:0.00} 格）");
    }
}
