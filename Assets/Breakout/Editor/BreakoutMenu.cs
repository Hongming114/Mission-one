using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Breakout.EditorTools
{
    /// <summary>
    /// 编辑器菜单。放在名字叫 Editor 的文件夹里的脚本只在编辑器里编译，
    /// 打包成 exe 时不会被包含（所以可以放心 using UnityEditor）。
    ///
    /// 顺便提供了一个命令行入口，可以不开 Unity 界面直接打包：
    ///   Unity.exe -quit -batchmode -nographics -projectPath &lt;工程路径&gt; ^
    ///             -executeMethod Breakout.EditorTools.BreakoutMenu.CliBuildWindows
    ///   想打成"能断点调试"的版本，再加一个开关（产物名会变成 Breakout3D-Debug.exe）：
    ///             ... -executeMethod Breakout.EditorTools.BreakoutMenu.CliBuildWindows -breakoutDebug
    /// </summary>
    public static class BreakoutMenu
    {
        const string SceneDir  = "Assets/Breakout/Scenes";
        const string ScenePath = SceneDir + "/Breakout.unity";

        // ===================== 菜单项 =====================

        [MenuItem("打砖块/生成场景并保存（推荐）", false, 0)]
        public static void CreateAndSaveScene()
        {
            if (!EditorUtility.DisplayDialog(
                    "打砖块",
                    "会新建一个空场景、用代码搭好整局游戏，然后保存到：\n" + ScenePath +
                    "\n\n如果这个文件已存在会被覆盖。继续？",
                    "继续", "取消"))
                return;

            if (CreateSceneInternal())
                EditorUtility.DisplayDialog("打砖块",
                    "搞定！\n\n场景：" + ScenePath + "\n已加入 Build Settings，直接按 Play 就能玩。", "好");
            else
                EditorUtility.DisplayDialog("打砖块", "场景保存失败，请看 Console 窗口。", "好");
        }

        [MenuItem("打砖块/只生成不保存（临时试玩）", false, 1)]
        public static void BuildInCurrentScene()
        {
            BreakoutBuilder.Build();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[打砖块] 已在当前场景生成（未保存）。按 Play 试玩，玩完直接不保存关掉即可。");
        }

        [MenuItem("打砖块/打开 Breakout 场景", false, 20)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("打砖块",
                    "还没有这个场景文件：\n" + ScenePath + "\n\n先执行『打砖块/生成场景并保存（推荐）』。", "好");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("打砖块/加入 Build Settings", false, 21)]
        public static void AddSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null || list[i].path != ScenePath) continue;
                list[i] = new EditorBuildSettingsScene(ScenePath, true);
                found = true;
                break;
            }
            if (!found) list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        [MenuItem("打砖块/删除生成物（清空场景）", false, 40)]
        public static void CleanScene()
        {
            GameObject root = GameObject.Find(BreakoutBuilder.RootName);
            if (root == null)
            {
                Debug.Log("[打砖块] 当前场景里没有 " + BreakoutBuilder.RootName);
                return;
            }
            Object.DestroyImmediate(root);
            Debug.Log("[打砖块] 已删除 " + BreakoutBuilder.RootName);
        }

        [MenuItem("打砖块/工程自检（检查关键类是否能找到）", false, 41)]
        public static void SelfCheck()
        {
            System.Type[] types =
            {
                typeof(GameManager), typeof(PaddleController), typeof(BallController),
                typeof(Brick), typeof(BreakoutHud), typeof(BreakoutBuilder), typeof(InputHelper),
            };
            int bad = 0;
            foreach (System.Type t in types)
            {
                if (t != null) Debug.Log("[打砖块] OK   " + t.FullName);
                else { bad++; Debug.LogError("[打砖块] 找不到某个类型"); }
            }
            Debug.Log(string.Format("[打砖块] 自检完成：成功 {0} 项，失败 {1} 项。", types.Length - bad, bad));
            EditorUtility.DisplayDialog("打砖块",
                string.Format("自检完成：\n\n成功 {0} 项\n失败 {1} 项\n\n详情看 Console 窗口。", types.Length - bad, bad), "好");
        }

        // ===================== 内部 / 命令行 =====================

        /// <summary>新建空场景 → 用代码搭好 → 存盘 → 加进 Build Settings。</summary>
        static bool CreateSceneInternal()
        {
            // 场景文件所在目录必须先真的存在，SaveScene 才不会失败
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Breakout", "Scenes"));
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BreakoutBuilder.Build();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("[打砖块] 场景保存失败：" + ScenePath);
                return false;
            }
            AssetDatabase.Refresh();
            AddSceneToBuildSettings();
            return true;
        }

        /// <summary>
        /// 命令行打包入口（不开 Unity 界面）：
        /// Unity.exe -quit -batchmode -nographics -projectPath &lt;工程&gt; -executeMethod Breakout.EditorTools.BreakoutMenu.CliBuildWindows -logFile build.log
        /// 产物在工程外的 Build/Windows/Breakout3D.exe。
        /// </summary>
        public static void CliBuildWindows()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.Log("[打砖块] 场景不存在，先自动生成：" + ScenePath);
                if (!CreateSceneInternal()) { EditorApplication.Exit(1); return; }
            }
            else
            {
                AddSceneToBuildSettings();
            }

            string projectDir = Directory.GetParent(Application.dataPath).FullName;
            string outDir = Path.Combine(projectDir, "Build", "Windows");
            Directory.CreateDirectory(outDir);

            bool wantDebug = HasArg("-breakoutDebug");

            BuildPlayerOptions opt = new BuildPlayerOptions();
            opt.scenes = new string[] { ScenePath };
            opt.locationPathName = Path.Combine(outDir, wantDebug ? "Breakout3D-Debug.exe" : "Breakout3D.exe");
            opt.target = BuildTarget.StandaloneWindows64;
            opt.options = wantDebug
                ? (BuildOptions.Development | BuildOptions.AllowDebugging)
                : BuildOptions.None;

            BuildReport report = BuildPipeline.BuildPlayer(opt);
            bool ok = report != null && report.summary.result == BuildResult.Succeeded;

            if (ok)
            {
                Debug.Log("[打砖块] 打包成功" + (wantDebug ? "（可调试版本）" : "（发布版本）") + "：" + opt.locationPathName);
                Debug.Log(string.Format("[打砖块] 体积 {0:F1} MB，耗时 {1:F1} 秒",
                    report.summary.totalSize / 1048576f, report.summary.totalTime.TotalSeconds));
            }
            else
            {
                Debug.LogError("[打砖块] 打包失败：" + (report == null ? "report == null" : report.summary.result.ToString()));
            }

            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>判断命令行里有没有带某个开关，例如 -batchmode、-breakoutDebug。</summary>
        static bool HasArg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}