using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DungeonCity.Core;
using DungeonCity.Gen;
using DungeonCity.View;

/// <summary>
/// 批处理自检入口（-executeMethod ProjectSetup.BatchAll）：
///   1) 建空场景 Assets/Scenes/Main.unity 并写进 Build Settings
///   2) 生成两个世界并跑 WorldValidator（不合法就抛异常）
///   3) 导出 II/III 类矩阵文本到 Docs/Generated
///   4) 用正交相机各渲一张 PNG（世界总览 + 出生房间近景），统计"粉色像素"比例来验证 shader 是否可用
///   5) 用 CellPathfinder 干跑可玩性：出生格 -> 梯子格可达、可站立格是否全部连通、刷怪判定若干次
/// 全部通过 EditorApplication.Exit(0)，否则 Exit(1)。
/// </summary>
public static class ProjectSetup
{
    const string SceneDir = "Assets/Scenes";
    const string ScenePath = "Assets/Scenes/Main.unity";

    static string ProjectRoot
    {
        get
        {
            string data = Application.dataPath;
            string dir = string.IsNullOrEmpty(data) ? "" : Path.GetDirectoryName(data);
            if (string.IsNullOrEmpty(dir)) dir = Directory.GetCurrentDirectory();   // 兜底：没有 Unity 环境（台架/桩）时用当前目录
            return dir;
        }
    }
    static string DocsDir => Path.Combine(ProjectRoot, "Docs");
    static string GenDir => Path.Combine(DocsDir, "Generated");

    // ============================================================ 自检日志落盘
    // 目的：把 Unity Console 的输出同时写到 Docs/Generated/Unity自检结果.txt，
    // 这样不需要开 Unity 也能（用一个文件）读到"这次自检做了哪几步、哪步失败、产物在哪、完整日志是什么"。

    class Step
    {
        public string name;
        public bool ok = true;
        public string note = "";
    }

    static readonly List<string> _captureLines = new List<string>();
    static readonly List<Step> _captureSteps = new List<Step>();
    static int _captureDepth;
    static int _captureErrors;
    static int _captureWarnings;

    /// <summary>跑一个自检步骤：捕获它（以及它调用的子步骤）产生的所有 Console 输出；最外层结束时落盘。</summary>
    static void RunCapture(string title, Action body)
    {
        bool outer = _captureDepth == 0;
        if (outer)
        {
            _captureLines.Clear();
            _captureSteps.Clear();
            _captureErrors = 0;
            _captureWarnings = 0;
            Application.logMessageReceived += OnLogMessage;
        }
        _captureDepth++;
        var step = new Step { name = title };
        _captureSteps.Add(step);
        _captureLines.Add("");
        _captureLines.Add("---------- " + title + " ----------");
        try
        {
            body();
        }
        catch (Exception ex)
        {
            step.ok = false;
            step.note = ex.GetType().Name + ": " + ex.Message;
            if (_captureDepth <= 1) Debug.LogException(ex);   // 嵌套时只在最外层记一次堆栈
            throw;
        }
        finally
        {
            _captureDepth--;
            if (_captureDepth == 0)
            {
                Application.logMessageReceived -= OnLogMessage;
                WriteCaptureResult();
            }
        }
    }

    static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        bool bad = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
        if (bad)
        {
            _captureErrors++;
            if (_captureSteps.Count > 0) _captureSteps[_captureSteps.Count - 1].ok = false;
        }
        else if (type == LogType.Warning) _captureWarnings++;

        string tag = type == LogType.Error ? "错误"
                   : type == LogType.Exception ? "异常"
                   : type == LogType.Assert ? "断言"
                   : type == LogType.Warning ? "警告" : "日志";
        _captureLines.Add("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] [" + tag + "] " + condition);
        if (bad && !string.IsNullOrEmpty(stackTrace))
        {
            string[] lines = stackTrace.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length && i < 24; i++)
            {
                string line = lines[i].TrimEnd();
                if (line.Length > 0) _captureLines.Add("        " + line);
            }
        }
    }

    static void WriteCaptureResult()
    {
        var sb = new StringBuilder();
        bool allOk = true;
        foreach (Step s in _captureSteps) if (!s.ok) allOk = false;

        sb.AppendLine("DungeonCity  Unity 自检结果（由 Assets/Editor/ProjectSetup.cs 自动生成）");
        sb.AppendLine("时间       : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("Unity 版本 : " + Application.unityVersion);
        sb.AppendLine("工程路径   : " + ProjectRoot);
        sb.AppendLine("批处理模式 : " + Application.isBatchMode);
        sb.AppendLine();
        sb.AppendLine("## 步骤清单");
        if (_captureSteps.Count == 0) sb.AppendLine("- （没有步骤记录）");
        foreach (Step s in _captureSteps)
            sb.AppendLine("- [" + (s.ok ? "通过" : "失败") + "] " + s.name + (string.IsNullOrEmpty(s.note) ? "" : "  —— " + s.note));
        sb.AppendLine();
        sb.AppendLine("## 结论");
        sb.AppendLine((allOk && _captureErrors == 0 ? "全部通过 ✅" : "存在问题 ❌") + "（错误 " + _captureErrors + " 条 / 警告 " + _captureWarnings + " 条）");
        sb.AppendLine();
        sb.AppendLine("## 产物（Docs/Generated）");
        try
        {
            Directory.CreateDirectory(GenDir);
            foreach (string f in Directory.GetFiles(GenDir))
            {
                var fi = new FileInfo(f);
                sb.AppendLine("- " + fi.Name + "  " + fi.Length + " 字节  " + fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
            }
        }
        catch (Exception ex) { sb.AppendLine("- （列目录失败：" + ex.Message + "）"); }
        sb.AppendLine();
        sb.AppendLine("## Console 全量输出");
        foreach (string line in _captureLines) sb.AppendLine(line);
        sb.AppendLine();
        sb.AppendLine("（文件结束）");

        try
        {
            Directory.CreateDirectory(GenDir);
            string path = Path.Combine(GenDir, "Unity自检结果.txt");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[ProjectSetup] 自检结果已写入 " + path);
        }
        catch (Exception ex) { Debug.LogError("[ProjectSetup] 写自检结果失败：" + ex.Message); }
    }


    [MenuItem("DungeonCity/生成场景")]
    public static void CreateMainScene() => RunCapture("生成场景", CreateMainSceneCore);

    static void CreateMainSceneCore()
    {
        Directory.CreateDirectory(Path.Combine(ProjectRoot, SceneDir));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log($"[ProjectSetup] 场景已创建 {ScenePath}（保存成功 = {ok}，场景可以为空：运行时由 GameBootstrap 自动搭好一切）");
    }

    [MenuItem("DungeonCity/1. 生成 + 自检世界")]
    public static void ValidateWorlds() => RunCapture("1. 生成 + 自检世界", ValidateWorldsCore);

    static void ValidateWorldsCore()
    {
        foreach (WorldKind kind in new[] { WorldKind.City, WorldKind.Dungeon })
        {
            WorldGenConfig cfg = WorldCatalog.For(kind);
            WorldData w = WorldGenerator.Generate(cfg, cfg.seed);
            List<string> errors = WorldValidator.Run(w);
            Debug.Log($"[ProjectSetup] {w.Describe()}\n{cfg.DumpIMatrix()}");
            if (errors.Count > 0)
            {
                foreach (string e in errors) Debug.LogError("[自检] " + e);
                throw new Exception($"{w.KindName} 世界自检失败，共 {errors.Count} 条");
            }
            Debug.Log($"[ProjectSetup] {w.KindName} 世界自检通过（{w.RoomCount} 个房间）");
        }
    }

    [MenuItem("DungeonCity/2. 导出矩阵")]
    public static void ExportMatrices() => RunCapture("2. 导出矩阵", ExportMatricesCore);

    static void ExportMatricesCore()
    {
        Directory.CreateDirectory(GenDir);
        List<string> files = MatrixDump.WriteAll(GenDir);
        foreach (string f in files) Debug.Log("[ProjectSetup] 已导出 " + f);
    }

    static Bounds BoundsOf(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    static Texture2D RenderToTexture(Camera cam, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        return tex;
    }

    static float PinkRatio(Texture2D tex)
    {
        Color32[] px = tex.GetPixels32();
        int pink = 0;
        foreach (Color32 c in px) if (c.r > 200 && c.g < 120 && c.b > 200) pink++;
        return (float)pink / px.Length;
    }

    [MenuItem("DungeonCity/3. 渲染预览 PNG")]
    public static void RenderPreviews() => RunCapture("3. 渲染预览 PNG", RenderPreviewsCore);

    static void RenderPreviewsCore()
    {
        Directory.CreateDirectory(GenDir);
        var mats = new MaterialLibrary();
        Shader baseShader = MaterialLibrary.BaseShader;   // 先取一次，触发懒加载，否则这里只会打印初始值 "(未找到)"
        Debug.Log($"[ProjectSetup] 使用的 shader = {MaterialLibrary.ShaderName}（{baseShader}）");

        GameObject root = new GameObject("临时预览根");
        GameObject lightGo = new GameObject("临时平行光");
        lightGo.transform.rotation = Quaternion.Euler(52f, -34f, 0f);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.shadows = LightShadows.None;
        UnityEngine.Rendering.AmbientMode prevMode = RenderSettings.ambientMode;
        Color prevAmbient = RenderSettings.ambientLight;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.70f);

        try
        {
            foreach (WorldKind kind in new[] { WorldKind.City, WorldKind.Dungeon })
            {
                WorldGenConfig cfg = WorldCatalog.For(kind);
                WorldData w = WorldGenerator.Generate(cfg, cfg.seed);
                WorldView view = WorldView.Build(w, mats, root.transform);
                view.SetVisible(true);

                CameraRig rig = CameraRig.Create("预览相机");

                // 世界总览
                rig.FrameBounds(view.WorldBounds());
                Texture2D over = RenderToTexture(rig.cam, 1440, 900);
                string path = Path.Combine(GenDir, $"{w.KindName}_世界总览.png");
                byte[] png = over.EncodeToPNG();
                if (png != null && png.Length > 0) File.WriteAllBytes(path, png);
                else Debug.LogWarning("[ProjectSetup] 总览图编码为空，跳过写文件：" + path);
                float ratio = PinkRatio(over);
                Debug.Log($"[ProjectSetup] {w.KindName} 总览图 = {path}  尺寸 {over.width}x{over.height}  粉色像素比例 = {ratio:P2}");
                UnityEngine.Object.DestroyImmediate(over);
                if (ratio > 0.05f) Debug.LogError($"[ProjectSetup] {w.KindName} 总览图粉色像素过多（{ratio:P2}）：材质 shader 可能没生效");

                // 出生房间近景
                RoomView rv = view.RoomViewAt(w.spawnRoom);
                if (rv != null && rv.root != null)
                {
                    Bounds cb = BoundsOf(rv.root);
                    cb.Expand(1.2f);
                    rig.FrameBounds(cb);
                    Texture2D near = RenderToTexture(rig.cam, 900, 700);
                    string path2 = Path.Combine(GenDir, $"{w.KindName}_出生房间.png");
                    byte[] png2 = near.EncodeToPNG();
                    if (png2 != null && png2.Length > 0) File.WriteAllBytes(path2, png2);
                    else Debug.LogWarning("[ProjectSetup] 出生房间图编码为空，跳过写文件：" + path2);
                    Debug.Log($"[ProjectSetup] {w.KindName} 出生房间图 = {path2}  粉色像素比例 = {PinkRatio(near):P2}  房间 {w.SpawnRoom}");
                    UnityEngine.Object.DestroyImmediate(near);
                }

                UnityEngine.Object.DestroyImmediate(view.root);
                UnityEngine.Object.DestroyImmediate(rig.gameObject);
            }
        }
        finally
        {
            RenderSettings.ambientMode = prevMode;
            RenderSettings.ambientLight = prevAmbient;
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(lightGo);
        }
    }

    [MenuItem("DungeonCity/4. 可玩性干跑（不进入 Play 模式）")]
    public static void DryRunPlayability() => RunCapture("4. 可玩性干跑（不进入 Play 模式）", DryRunPlayabilityCore);

    static void DryRunPlayabilityCore()
    {
        WorldData city = WorldGenerator.Generate(WorldCatalog.City(), WorldCatalog.City().seed);
        WorldData dungeon = WorldGenerator.Generate(WorldCatalog.Dungeon(), WorldCatalog.Dungeon().seed);

        foreach (WorldData w in new[] { city, dungeon })
        {
            int standable = 0;
            foreach (RoomData r in w.AllRooms)
                for (int x = 0; x < r.width; x++)
                    for (int y = 0; y < r.height; y++)
                        if (r.CanStand(x, y)) standable++;

            Dictionary<CellPathfinder.Node, int> d = CellPathfinder.Distances(w, w.spawnRoom, w.spawnCell);
            int reachableCells = d.Count;
            int toLadder = CellPathfinder.StepsTo(w, w.spawnRoom, w.spawnCell, w.worldPortalRoom, w.worldPortalCell);
            Debug.Log($"[干跑] {w.KindName}：可站立格 {standable} 个，从出生格可达 {reachableCells} 个（含门格传送到相邻房间后继续走）；出生格 -> 梯子格 = {(toLadder < 0 ? "不可达！" : toLadder + " 步")}");
            if (toLadder < 0) Debug.LogError($"[干跑] {w.KindName} 的出生格走不到梯子格，地图不可玩");

            Dictionary<Vector2Int, int> dist = RoomDistanceField.Distances(w, w.spawnRoom);
            int far = 0;
            foreach (KeyValuePair<Vector2Int, int> kv in dist) if (kv.Value >= WorldCatalog.For(w.kind).monsterMinRoomDistance) far++;
            Debug.Log($"[干跑] {w.KindName}：距出生房间 >= {WorldCatalog.For(w.kind).monsterMinRoomDistance} 的房间 {far} / {w.RoomCount} 个");
        }

        // 刷怪判定：模拟 5 次（每次推进 61 秒）
        var director = new MonsterSpawnDirector(WorldCatalog.Dungeon());
        int count = 0;
        for (int i = 0; i < 5; i++)
        {
            MonsterSpawnPlan plan = director.Tick(61f, dungeon, dungeon.spawnRoom, count);
            if (plan == null) { Debug.Log($"[干跑] 第 {i + 1} 次没有到期"); continue; }
            count += plan.MonsterCount;
            Debug.Log($"[干跑] 第 {i + 1} 次判定 -> 新生成 {plan.MonsterCount} 个，累计 {count} 个\n{plan.Describe()}");
        }
        Debug.Log("[干跑] 刷怪上限系数 = " + WorldCatalog.Dungeon().monsterCapRoomsFactor + "，上限 = " + (WorldCatalog.Dungeon().monsterCapRoomsFactor * dungeon.RoomCount) + " 个");
    }

    [MenuItem("DungeonCity/全部自检（场景 + 世界 + 矩阵 + 渲染 + 干跑）")]
    public static void BatchAll()
    {
        int code = 0;
        RunCapture("全部自检（场景 + 世界 + 矩阵 + 渲染 + 干跑）", () => { code = BatchAllCore(); });
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    static int BatchAllCore()
    {
        try
        {
            Debug.Log("[ProjectSetup] ===== 开始全部自检 =====");
            CreateMainScene();
            ValidateWorlds();
            ExportMatrices();
            RenderPreviews();
            DryRunPlayability();
            Debug.Log("[ProjectSetup] ===== 全部自检完成，无异常 =====");
            return 0;
        }
        catch (Exception e)
        {
            Debug.LogError("[ProjectSetup] 自检失败：" + e);
            return 1;
        }
    }
}
