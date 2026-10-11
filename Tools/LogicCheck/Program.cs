using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DungeonCity.Core;
using DungeonCity.Gen;

/// <summary>
/// 在 Unity 编辑器跑不起来的沙箱里，用 dotnet + UnityEngine 桩真实跑一遍 Core/Gen：
/// 生成两个世界 -> 自检 -> 打印 II/III 矩阵 -> 房间连通性 -> 门/梯子 -> 格子级可达性 ->
/// 刷怪规则 v2 干跑 -> 导出矩阵文本。详细内容写进 Docs/Generated/自检报告.txt，控制台只打要点。
/// </summary>
static class Program
{
    static readonly List<string> report = new List<string>();
    static int fails;

    static void Report(string s = "")
    {
        report.Add(s);
    }

    static void Echo(string s = "")
    {
        report.Add(s);
        Console.WriteLine(s);
    }

    static void Check(bool ok, string what)
    {
        if (ok) Echo("  [通过] " + what);
        else { fails++; Echo("  [失败] " + what); }
    }

    static string FindProjectRoot()
    {
        DirectoryInfo d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null)
        {
            if (Directory.Exists(Path.Combine(d.FullName, "Assets"))) return d.FullName;
            d = d.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    static string Indent(string s) => "    " + s.Replace("\n", "\n    ");

    static string JoinPath(List<CellPathfinder.Node> path, int start, int count)
    {
        if (path == null || path.Count == 0) return "（空）";
        var list = new List<string>();
        for (int i = Math.Max(0, start); i < Math.Min(path.Count, start + count); i++) list.Add(path[i].ToString());
        return string.Join(" -> ", list);
    }

    static int CountStandable(WorldData w)
    {
        int n = 0;
        foreach (RoomData r in w.AllRooms)
            for (int x = 0; x < r.width; x++)
                for (int y = 0; y < r.height; y++)
                    if (r.CanStand(x, y)) n++;
        return n;
    }

    static int CountTiles(WorldData w, TileCode code)
    {
        int n = 0;
        foreach (RoomData r in w.AllRooms)
            for (int x = 0; x < r.width; x++)
                for (int y = 0; y < r.height; y++)
                    if ((TileCode)r.Tile(x, y) == code) n++;
        return n;
    }

    static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        string root = FindProjectRoot();
        string outDir = args.Length > 0 ? args[0] : Path.Combine(root, "Docs", "Generated");
        Directory.CreateDirectory(outDir);

        Echo("======= DungeonCity 世界生成逻辑自检（dotnet 版，不含 Unity 渲染/UI/输入）=======");
        Echo("项目根：" + root);
        Echo("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Echo();

        WorldGenConfig cityCfg = WorldCatalog.City();
        WorldGenConfig dungeonCfg = WorldCatalog.Dungeon();
        WorldData city = WorldGenerator.Generate(cityCfg, cityCfg.seed);
        WorldData dungeon = WorldGenerator.Generate(dungeonCfg, dungeonCfg.seed);

        foreach (WorldData w in new[] { city, dungeon })
        {
            WorldGenConfig cfg = w.kind == WorldKind.City ? cityCfg : dungeonCfg;
            Echo($"=== {w.KindName}（M = {(int)w.kind}，种子 {w.seed}）===");
            Report("I 类矩阵（数值 = 房间类型；0 无房间 / 1 城市房间 / 2 地牢房间）：");
            Report(cfg.DumpIMatrix());
            Report(w.Describe());
            Report();

            List<string> errors = WorldValidator.Run(w);
            if (errors.Count == 0) Check(true, $"{w.KindName} 世界自检：0 条问题");
            else
            {
                Report("自检问题清单：");
                foreach (string e in errors) Report("   · " + e);
                Check(false, $"{w.KindName} 世界自检：{errors.Count} 条问题");
            }

            RoomData sr = w.SpawnRoom;
            int sx = w.spawnCell.x, sy = w.spawnCell.y;
            Echo($"  出生：房间{sr.Key} {sr.width}x{sr.height} 格({sx},{sy}) II={sr.Tile(sx, sy)}({TileSymbol.Of(sr.Tile(sx, sy))}) III={FuncCode.Label(sr.Func(sx, sy))}");
            Check(FuncCode.CanStand(sr.Func(sx, sy)), "出生格 III != 0（非不可达格）");
            Check(!FuncCode.IsRoomPortal(sr.Func(sx, sy)) && !FuncCode.IsWorldPortal(sr.Func(sx, sy)), "出生格既非门格也非梯子格");

            RoomData lr = w.WorldPortalRoom;
            int lx = w.worldPortalCell.x, ly = w.worldPortalCell.y;
            Echo($"  梯子：房间{lr.Key} 格({lx},{ly}) II={lr.Tile(lx, ly)}({TileSymbol.Of(lr.Tile(lx, ly))}) III={FuncCode.Label(lr.Func(lx, ly))}");
            Check(FuncCode.IsWorldPortal(lr.Func(lx, ly)), "梯子格 III = 2.2");
            int ladderTiles = CountTiles(w, TileCode.Ladder);
            Check(ladderTiles == 1, $"II = 3 的梯子格唯一（实测 {ladderTiles} 个）");

            int brokenRooms = 0;
            foreach (RoomData r in w.AllRooms)
                if (!RoomConnectivity.IsConnected(r))
                {
                    brokenRooms++;
                    Report($"   · 房间{r.Key} 内部不连通：可站立 {RoomConnectivity.StandableCount(r)} / 可达 {RoomConnectivity.ReachableCount(r)}");
                }
            Check(brokenRooms == 0, $"每个房间内部四连通（共 {w.RoomCount} 个房间）");

            int doorCells = CountTiles(w, TileCode.Door);
            Check(doorCells == w.portals.Count * 2, $"门格数 == portal 数 * 2（门格 {doorCells}，portal {w.portals.Count}）");

            int standable = CountStandable(w);
            Dictionary<CellPathfinder.Node, int> reached = CellPathfinder.Distances(w, w.spawnRoom, w.spawnCell);
            int stepsToLadder = CellPathfinder.StepsTo(w, w.spawnRoom, w.spawnCell, w.worldPortalRoom, w.worldPortalCell);
            Echo($"  可站立格 {standable} 个；从出生格出发可达 {reached.Count} 格；出生格 -> 梯子格 = {(stepsToLadder < 0 ? "不可达！" : stepsToLadder + " 步")}");
            Check(stepsToLadder >= 0, "出生格能走到梯子格（地图可玩）");
            Check(reached.Count == standable, "所有可站立格都能从出生格走到（没有孤岛房间）");

            List<CellPathfinder.Node> path = CellPathfinder.Path(w, w.spawnRoom, w.spawnCell, w.worldPortalRoom, w.worldPortalCell);
            Report($"  最短路径共 {path.Count} 个格子节点（含门格传送）：");
            Report("    开头：" + JoinPath(path, 0, 8));
            Report("    结尾：" + JoinPath(path, path.Count - 8, 8));

            Dictionary<Vector2Int, int> rd = RoomDistanceField.Distances(w, w.spawnRoom);
            var parts = new List<string>();
            foreach (KeyValuePair<Vector2Int, int> kv in rd) parts.Add($"({kv.Key.x},{kv.Key.y})={kv.Value}");
            parts.Sort();
            int far = 0;
            foreach (KeyValuePair<Vector2Int, int> kv in rd) if (kv.Value >= cfg.monsterMinRoomDistance) far++;
            Echo($"  距出生房间 >= {cfg.monsterMinRoomDistance} 的房间：{far} / {w.RoomCount}");
            Report("  各房间到出生房间的距离：" + string.Join("，", parts));
            Report();

            // 各个房间的尺寸/装饰随机性抽查（同类型房间要有差异）
            var sizeParts = new List<string>();
            foreach (RoomData r in w.AllRooms)
            {
                int rocks = 0;
                for (int x = 0; x < r.width; x++)
                    for (int y = 0; y < r.height; y++)
                        if (FuncCode.IsBlocked(r.Func(x, y))) rocks++;
                sizeParts.Add($"{r.Key}={r.width}x{r.height}/不可达{rocks}格");
            }
            Echo("  房间尺寸与内部障碍：" + string.Join("  ", sizeParts));
        }

        Echo();
        Echo("=== 刷怪规则 v2 干跑（地牢世界，玩家房间 = 出生房间）===");
        Echo($"  参数：每房抽样 {dungeonCfg.monsterCellSampleRatio:P0}（向下取整）　每格概率 P = {dungeonCfg.monsterSpawnChanceP}　最小房间距离 ≥ {dungeonCfg.monsterMinRoomDistance}");
        Echo($"  判定周期 t = {dungeonCfg.monsterJudgeIntervalSeconds}s　场上怪 <= {dungeonCfg.monsterForceJudgeMaxCount} 立即判定（最小间隔 {dungeonCfg.monsterForceJudgeMinInterval}s）　上限 = {dungeonCfg.monsterCapRoomsFactor} × 房间数 = {dungeonCfg.monsterCapRoomsFactor * dungeon.RoomCount}");

        var director = new MonsterSpawnDirector(dungeonCfg);
        int total = 0;
        for (int i = 1; i <= 6; i++)
        {
            MonsterSpawnPlan plan = director.Tick(61f, dungeon, dungeon.spawnRoom, total);
            if (plan == null) { Echo($"  第 {i} 次：还没到判定时间"); continue; }
            total += plan.MonsterCount;
            Echo($"  第 {i} 次判定：新生成 {plan.MonsterCount} 个，累计 {total} 个");
            Report(Indent(plan.Describe()));
            foreach (MonsterSpawnPoint pt in plan.points)
            {
                RoomData r = dungeon.Room(pt.room);
                Report($"     · 怪 @ 房间({pt.room.x},{pt.room.y}) 格({pt.cell.x},{pt.cell.y}) 房间距离 {pt.roomDistance}　III={FuncCode.Label(r.Func(pt.cell.x, pt.cell.y))}");
            }
        }
        Check(total > 0, "6 次判定至少生成 1 只怪（P 很低但抽样格子足够多）");

        var d2 = new MonsterSpawnDirector(dungeonCfg);
        MonsterSpawnPlan early = d2.Tick(1f, dungeon, dungeon.spawnRoom, 0);
        Check(early == null, "场上怪 = 0 且离上次判定只有 1s（< 最小间隔）时不判定");
        float waited = 1f;
        MonsterSpawnPlan forced = null;
        while (waited < 40f)
        {
            forced = d2.Tick(1f, dungeon, dungeon.spawnRoom, 0);
            waited += 1f;
            if (forced != null) break;
        }
        Check(forced != null, $"场上怪 <= {dungeonCfg.monsterForceJudgeMaxCount} 时立即判定（实测等待 {waited}s，而非等满 {dungeonCfg.monsterJudgeIntervalSeconds}s）");
        Report("  立即判定结果：" + (forced == null ? "无" : forced.Describe()));

        var d3 = new MonsterSpawnDirector(dungeonCfg);
        int cap = dungeonCfg.monsterCapRoomsFactor * dungeon.RoomCount;
        MonsterSpawnPlan capped = d3.Tick(61f, dungeon, dungeon.spawnRoom, cap);
        Echo("  上限测试（场上怪 = 上限 " + cap + "）：" + (capped == null ? "未判定" : $"判定通过 {capped.rolledSuccesses} 次，实际新增 {capped.MonsterCount} 个，被压制 {capped.suppressedByCap} 次"));
        Check(capped != null && capped.capped && capped.MonsterCount == 0, "达到上限时仍然判定，但成功也不生成（capped = true，新增 0）");

        // 抽样格子数核对：每个符合距离条件的房间抽 floor(非不可达格数 * 10%)
        Dictionary<Vector2Int, int> pd = RoomDistanceField.Distances(dungeon, dungeon.spawnRoom);
        int expectSampled = 0;
        foreach (RoomData r in dungeon.AllRooms)
        {
            int dd;
            if (!pd.TryGetValue(r.grid, out dd)) continue;
            if (dd < dungeonCfg.monsterMinRoomDistance) continue;
            int pool = 0;
            for (int x = 0; x < r.width; x++)
                for (int y = 0; y < r.height; y++)
                    if (FuncCode.CanStand(r.Func(x, y))) pool++;
            expectSampled += (int)Math.Floor(pool * dungeonCfg.monsterCellSampleRatio);
        }
        var d4 = new MonsterSpawnDirector(dungeonCfg);
        MonsterSpawnPlan sample = d4.Tick(61f, dungeon, dungeon.spawnRoom, 99999);
        Echo($"  抽样格子核对：判定里抽了 {sample.sampledCells} 格，按 floor(非不可达格 × 10%) 累加应为 {expectSampled} 格");
        Check(sample.sampledCells == expectSampled, "每个够距离的房间都按 10%（向下取整）抽样");
        Check(sample.suppressedByCap == sample.rolledSuccesses && sample.MonsterCount == 0, "上限之上：所有判定通过都被压制（不生成）");

        Echo();
        SimTests.Run(city, dungeon, Check, Echo);

        Echo("=== 导出矩阵文本 ===");
        List<string> files = MatrixDump.WriteAll(outDir);
        foreach (string f in files) Echo("  已写出 " + f);
        Check(files.Count >= 2, "矩阵导出文件已生成");

        Echo();
        Echo(fails == 0 ? "==> 全部检查通过 ✅" : $"==> 有 {fails} 项检查失败 ❌");
        report.Add("");
        report.Add(fails == 0 ? "全部检查通过" : $"有 {fails} 项检查失败");
        string reportPath = Path.Combine(outDir, "自检报告.txt");
        File.WriteAllText(reportPath, string.Join(Environment.NewLine, report) + Environment.NewLine, new UTF8Encoding(false));
        Console.WriteLine("详细报告：" + reportPath);
        return fails == 0 ? 0 : 1;
    }
}