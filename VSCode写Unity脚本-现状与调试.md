# VS Code 写 Unity C# —— 现状、用法与排错

> 适用工程：`C:\Users\chair\Desktop\ds工作区\unity_breakout`
> 记录时间：2026-10-04 00:05

## 0. 一句话

这台机器上 **「Unity 指向 VS Code」** 和 **「VS Code 的 C# 环境」** 都已经配好、并且已经实测跑通（证据见第 1 节）。
本文剩下的价值是：**日常怎么用**（第 2 节）、**Unity 自动生成的那些文件是什么**（第 3 节）、**出问题查哪里**（第 5 节）。

---

## 1. 当前实际状态（每条都能自己复核）

| 项目 | 状态 | 证据 |
|---|---|---|
| Unity 的外部脚本编辑器 | ✅ 已指向 VS Code | 注册表 `HKCU\Software\Unity Technologies\Unity Editor 5.x`，值名 `kScriptsDefaultApp_h2657262712`（REG_BINARY，内容是 ANSI 字节 + 结尾 00）解码 = `C:\Users\chair\AppData\Local\Programs\Microsoft VS Code\Code.exe` |
| Unity 是不是真认了这个编辑器 | ✅ 认了 | 只有当外部编辑器是 VS Code 时，Unity 才会往工程里写 `.vscode\`；该目录三个文件时间戳都是 2026-10-04 00:04:15 |
| VS Code 的 C# 扩展 | ✅ 已装 | `%USERPROFILE%\.vscode\extensions\` 下有 `ms-dotnettools.csdevkit-3.40.210-win32-x64`、`ms-dotnettools.csharp-2.160.4-win32-x64`、`ms-dotnettools.vscode-dotnet-runtime-3.2.0` |
| Unity 官方 VS Code 扩展 | ✅ 已装 | `visualstudiotoolsforunity.vstuc-1.3.2` |
| 解决方案能不能加载 | ✅ 能 | `%APPDATA%\Code\logs\20261004T000415\window1\exthost\ms-dotnettools.csdevkit\C# Dev Kit.log`：`Default solution: unity_breakout.slnx` → `Completed: processing the solution file ...` |
| 依赖能不能还原 | ✅ 能 | 同一日志：`Starting: command: "C:\Program Files\dotnet\dotnet.exe" restore ...\unity_breakout.slnx` → `Completed` |
| 两个 C# 项目能不能加载 | ✅ 能 | `C#.log`：`[WorkspaceProjectFactoryService] 项目 ...\Assembly-CSharp.csproj 由 C# 开发工具包加载`，下一行同款 `Assembly-CSharp-Editor.csproj` |
| Roslyn 语言服务 | ✅ 起来了 | `C#.log`：`Activating C# + C# Dev Kit...` / `Language server initialized` / `Locating .NET runtime version 10.0.5` / `Dotnet path: C:\Program Files\dotnet\dotnet.exe` |
| 上面这些日志里的 error 条数 | **0** | 只有 1 条 warning，说的是 Roslyn 自己的临时项目 `Canonical.csproj 具有未解析的依赖项`，跟你的工程无关 |

---

## 2. 日常怎么用（四个动作）

### 动作 1：打开工程

双击 `打开工程-VSCode和Unity.cmd`，或者手动：

```powershell
& "$env:LOCALAPPDATA\Programs\Microsoft VS Code\Code.exe" "C:\Users\chair\Desktop\ds工作区\unity_breakout"
```

VS Code 会记住这个文件夹，下次从「最近打开」直接进。

### 动作 2：写代码

* 打开 `Assets\Breakout\Scripts\` 下任意 `.cs`（比如 `BallController.cs`），补全、跳转到定义（F12）、重命名（F2）、看引用（Shift+F12）都能用。
* Unity 的 API 提示（`MonoBehaviour`、`Vector3`、`GetComponent`…）来自 `.csproj` 里对 `Library\ScriptAssemblies\*.dll` 和 Unity 引擎 DLL 的引用，Unity 会自动维护，**不用手动配**。
* 保存（Ctrl+S）后切回 Unity，Unity 自动重新编译，右下角转圈。

### 动作 3：断点调试（这才是用 VS Code 的最大理由）

1. **先在 Unity 里按 ▶ 进入 Play 模式**（没进 Play 模式就附加，会立刻断开）。
2. 回到 VS Code，在 `.cs` 行号左边点一下，出现**红色实心圆点**（空心灰圈 = 断点没绑上，见第 5 节）。
3. 按 `F5`，选 `Attach to Unity`（这个配置就是 `.vscode\launch.json` 里的那条）。
4. 游戏跑到那一行会停住，左侧能看变量、调用栈、逐行单步（F10）/ 步入（F11）。
5. 结束：`Shift+F5` 断开；别忘了回 Unity 再按一次 ▶ 退出 Play 模式。

> 补充：也可以用 Unity 侧的方式调试 —— VS Code 里按 `Ctrl+Shift+P` → `.NET: ...` 系列命令。日常用 F5 就够。

### 动作 4：改完回 Unity

* 编译错误看 **Unity 的 Console 面板**（不是 VS Code 的"问题"面板 —— Unity 才是真正的编译器）。
* VS Code 里看到的一堆红波浪线，如果 Unity Console 是干净的，多数只是语言服务还没跟上，等两秒或 `Ctrl+Shift+P` → `Developer: Reload Window`。

---

## 3. `.vscode\` 里这三个文件是什么（都是 Unity 自动写的）

| 文件 | 大小 | 作用 |
|---|---|---|
| `extensions.json` | 75 B | 工作区推荐扩展。原本 Unity 只写 `visualstudiotoolsforunity.vstuc`，我补上了 `ms-dotnettools.csdevkit` |
| `launch.json` | 178 B | 定义了一条名为 `Attach to Unity` 的调试配置，`"type": "vstuc"` 由 Unity 扩展提供。按 F5 用的就是它 |
| `settings.json` | 1836 B | 工作区设置，见下 |

`settings.json` 里四个关键项：

* `files.exclude` —— 把 `Library/`、`Temp/`、`Build/`、`ProjectSettings/`、`**/*.meta`、`**/*.unity`、`**/*.png` 等从左侧资源管理器里藏掉。**这是"隐藏"，不是"删除"。**
* `files.associations` —— 让 `.unity` / `.prefab` / `.asset` / `.meta` 按 YAML 高亮。
* `explorer.fileNesting` —— `Assembly-CSharp.csproj` 会折叠到 `unity_breakout.slnx` 下面，侧边栏不会一排 csproj。
* `dotnet.defaultSolution: "unity_breakout.slnx"` —— **C# Dev Kit 的设置项**，告诉它加载哪个解决方案。这一条也是为什么必须装 C# Dev Kit（只装老的 C# 扩展不认这个设置）。

---

## 4. 三个容易困惑的点

* **为什么是 `.slnx` 而不是 `.sln`？**
  这是新版工具链用的 XML 格式解决方案，比老 `.sln` 简洁可读。本机 .NET SDK 10.0.401 和 C# Dev Kit 3.40 都支持（第 1 节已实测 restore 成功）。
  `unity_breakout.slnx` 内容就三行：
  ```xml
  <Solution>
    <Project Path="Assembly-CSharp.csproj" />
    <Project Path="Assembly-CSharp-Editor.csproj" />
  </Solution>
  ```

* **Unity 会覆盖 `.vscode\`**：每次 Unity 生成工程文件时都会去"patch"这三个文件。想自己大改不被冲掉，就在工程根目录建一个**空文件** `.vstupatchdisable`，Unity 就不碰了。

* **改外部编辑器只在 Unity 启动时生效**：这个设置存在 `EditorPrefs`（Windows 上就是那个注册表键）里，Unity 启动时读一次。改完要**完全退出 Unity 再打开**。

---

## 5. 报错 / 现象对照表

| 现象 | 原因 | 处理 |
|---|---|---|
| `.cs` 里 `UnityEngine` 找不到、一片红波浪线 | Unity 还没生成 `.csproj`，或语言服务还在加载 | 先等 10 秒；不行就 Unity 菜单 `Edit > Preferences > External Tools`，勾 `Regenerate project files` 后点 `Regenerate project files` |
| 提示找不到 .NET SDK / Runtime | SDK 路径没被识别 | 本机在 `C:\Program Files\dotnet\dotnet.exe`（10.0.401）；`Ctrl+Shift+P` → `Developer: Reload Window`，或跑 `.NET: Install .NET SDK` |
| 按 F5 说"无法附加到 Unity / 找不到进程" | Unity 没在 Play 模式 | 先在 Unity 里按 ▶，再回 VS Code 按 F5 |
| 断点是**空心灰圈**、提示"未绑定断点" | 附加上了，但那行代码没进当前编译的 DLL | 确认 Unity Console 无红字、编译完成，再重新 F5 |
| 左侧看不到 `.csproj` / `.meta` / `Library` | 被 `files.exclude` 藏了（正常行为） | 想临时看到，就把 `settings.json` 里对应项改成 `false` |
| 在 Unity 里双击 `.cs` 还是开 Visual Studio | 设置只在下次启动 Unity 时生效 | 完全退出 Unity 再打开 |
| Unity 说"此项目已被另一个实例打开" | 残留锁文件 | 确认没有 Unity 进程后删掉 `Temp\UnityLockfile` |
| VS Code 里 C# 面板显示一堆 `CS0246`，但 Unity 能编译 | 用的是 VS Code 自己解析的结果，Unity 用的是自己的编译器 | 以 Unity Console 为准；先 `Regenerate project files` 再重载窗口 |

---

## 6. 想从头重来一遍时的命令

```powershell
$proj = "C:\Users\chair\Desktop\ds工作区\unity_breakout"

# (1) 让 Unity 重新生成工程文件：
#     Unity 菜单 Edit > Preferences > External Tools > Regenerate project files

# (2) 让 .vscode 回到出厂状态（删掉后重启 Unity 会自动重建）
Remove-Item "$proj\.vscode" -Recurse -Force

# (3) 手动装 VS Code 扩展（一般不需要，已经装好了）
& "$env:LOCALAPPDATA\Programs\Microsoft VS Code\bin\code.cmd" --install-extension ms-dotnettools.csdevkit
& "$env:LOCALAPPDATA\Programs\Microsoft VS Code\bin\code.cmd" --install-extension visualstudiotoolsforunity.vstuc

# (4) 命令行验证解决方案和两个项目都在
& "C:\Program Files\dotnet\dotnet.exe" sln "$proj\unity_breakout.slnx" list
```

期望输出：`Assembly-CSharp-Editor.csproj` 和 `Assembly-CSharp.csproj` 两行。
---

## 7. 不打开 Unity 界面，能不能在 VS Code 里跑游戏？

### 7.1 先把话说准

| 你想要的 | 行不行 | 为什么 |
| --- | --- | --- |
| 在 VS Code 里按一个键，**像 Unity 的 ▶ 那样**跑 Play 模式 | ❌ **不行** | Play 模式是 Unity 编辑器进程内部的行为。`.csproj` 只是一份"源码清单 + 引用清单"，它引用的 `UnityEngine.*.dll` 是引擎本体，输出目录还是 `Temp\obj\`。`dotnet run` 会立刻失败——没有引擎运行时，这些类加载不起来。VS Code 在这里只是**编辑器 + 调试器客户端**，里面没有 Unity。 |
| 在 VS Code 里按一个键，**把游戏打包成 exe 并运行**，全程不出 Unity 窗口 | ✅ **可以** | 用 `Unity.exe -batchmode -nographics` 在后台无界面打包。Unity.exe 确实在跑，但没有窗口，你看不到它。 |

所以准确的说法是：**能做到"不打开 Unity 界面"，做不到"不运行 Unity"。**
（另一条路也要 Unity：VS Code 连的是正在 Play 的 Unity 编辑器进程。）

### 7.2 已经给你配好了，怎么用

在 `.vscode\` 里放了 `build-breakout.ps1` 和 `tasks.json`：

* **`Ctrl+Shift+B`** → 打包发布版 `Build\Windows\Breakout3D.exe`
* **`Ctrl+Shift+P`** → 输入 `Tasks: Run Task` → 可选：
  * `打砖块: 打包 exe（可调试版）` → 产物 `Breakout3D-Debug.exe`（带 Development + AllowDebugging）
  * `打砖块: 打包并运行（可调试版）` → 打包完直接启动 exe
  * `打砖块: 只运行已打好的可调试版`
  * `打砖块: 打开打包日志 build.log`

不想开 VS Code 也行，命令行等价写法：

```powershell
$proj = "C:\Users\chair\Desktop\ds工作区\unity_breakout"
& "C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe" `
  -quit -batchmode -nographics `
  -projectPath $proj `
  -executeMethod Breakout.EditorTools.BreakoutMenu.CliBuildWindows `
  -logFile "$proj\build.log"

# 想打成可以断点调试的版本，就在末尾再加一个开关：
#   -breakoutDebug        （产物变成 Breakout3D-Debug.exe）
```

### 7.3 三条硬性前提（踩了必失败）

1. **打包前必须完全退出 Unity 编辑器。** 一个工程同时只能被一个 Unity 实例占用，否则报"另一个实例已打开此项目"。脚本会先检测并直接告诉你。
2. **`Temp\UnityLockfile` 残留。** 上次 Unity 异常退出（比如崩溃、被强制结束）会留下它。脚本在确认没有 Unity 进程后会自动删掉。
3. **第一次打包很慢。** 命令行冷启动 Unity + 全量导入资产，第一次可能几分钟；之后增量，通常几十秒。日志在 `build.log`。

### 7.4 调试那个 exe（断点）

1. 用 `打砖块: 打包并运行（可调试版）` 打包并启动 exe。
2. 回 VS Code，`Ctrl+Shift+P` → 输入 **`Attach Unity Debugger`** → 在列表里选刚启动的那个播放器。
   （这是 Unity 官方扩展 `vstuc` 提供的命令，它会自动发现正在运行的编辑器和播放器。）
3. 打开 `.cs` 下断点，命中即可。

> **不要**自己往 `launch.json` 里写死 `"endPoint": "127.0.0.1:56000"` 这种端口。我在本机所有 Unity 二进制（`Unity.exe`、`UnityPlayer.dll` 的 6 个变体、`WindowsPlayer.exe`）里搜过 `56000` / `54998` / `debuggerPort`，**零命中**——端口是运行时协商的，写错只会得到一个"连不上"。用命令面板那条路最稳。

### 7.5 什么时候值得这么干

* **值得**：想"改完代码 → 一键出 exe → 直接跑"，不想让 Unity 编辑器一直开着吃内存；或者要给不会用 Unity 的人一个 exe。
* **不值得**：日常调手感。Play 模式改代码即时生效（秒级域重载），而命令行打包每次都要冷启动 Unity。
* 一句话：**写代码、调手感用 Play 模式；出成品、给别人玩用命令行打包。**