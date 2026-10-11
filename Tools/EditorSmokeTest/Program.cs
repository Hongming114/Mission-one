using System;
using System.IO;

// 目的：在没有 Unity 的环境里，用 CompileCheck 的 Unity 桩真的跑一遍
// ProjectSetup.BatchAll()，验证"自检日志落盘"这条路（Docs/Generated/Unity自检结果.txt）
// 真的会被写出来、内容结构正确 —— 这样 Unity 里的菜单/批处理就只是一次同样的调用。
static class Program
{
    static string FindRoot()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12 && dir != null; i++)
        {
            if (File.Exists(Path.Combine(dir, "Assets", "Editor", "ProjectSetup.cs"))) return dir;
            DirectoryInfo up = Directory.GetParent(dir);
            dir = up == null ? null : up.FullName;
        }
        return Environment.CurrentDirectory;
    }

    static int Main(string[] args)
    {
        string root = FindRoot();
        Environment.CurrentDirectory = root;      // ProjectSetup 用的是相对路径 Docs/Generated
        Console.WriteLine("工程根目录 = " + root);

        string outFile = Path.Combine(root, "Docs", "Generated", "Unity自检结果.txt");
        if (File.Exists(outFile)) File.Delete(outFile);

        int code = -1;
        Console.WriteLine("=== 调用 ProjectSetup.BatchAll()（桩环境）===");
        try { ProjectSetup.BatchAll(); code = 0; }
        catch (Exception e) { Console.WriteLine("BatchAll 抛出：" + e.GetType().Name + ": " + e.Message); code = 2; }

        Console.WriteLine("BatchAll 返回 = " + code + "，结果文件存在 = " + File.Exists(outFile));
        if (!File.Exists(outFile))
        {
            Console.WriteLine("!! 自检结果文件没有被写出 —— 落盘逻辑有问题");
            return 3;
        }

        string text = File.ReadAllText(outFile);
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        Console.WriteLine("结果文件字节 = " + new FileInfo(outFile).Length + "，行数 = " + lines.Length);
        Console.WriteLine("---- 前 40 行 ----");
        for (int i = 0; i < lines.Length && i < 40; i++) Console.WriteLine(lines[i]);
        Console.WriteLine("---- 末 8 行 ----");
        for (int i = Math.Max(0, lines.Length - 8); i < lines.Length; i++) Console.WriteLine(lines[i]);

        // 这是"桩环境跑出来的"证据，改名保留，避免与真正的 Unity 运行结果混淆
        string keep = Path.Combine(root, "Docs", "Generated", "Unity自检结果_桩试跑.txt");
        if (File.Exists(keep)) File.Delete(keep);
        File.Move(outFile, keep);
        Console.WriteLine("已重命名为 " + keep);
        return 0;
    }
}