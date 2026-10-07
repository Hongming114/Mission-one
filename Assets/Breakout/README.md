# Breakout（打砖块）—— Assets/Breakout 说明

这是一个"纯代码搭场景"的最小 Unity 3D 打砖块工程，用来学 Unity 的基本套路。
配套的入门文档在工程根目录旁边的 `Unity打砖块-使用与Csharp入门.md`。

## 怎么跑起来

方式一（推荐，双击就能玩）：
1. Unity Hub → Open → 选 `unity_breakout` 文件夹；
2. 等右下角的编译转圈结束（第一次要 1~3 分钟）；
3. 左侧 Project 窗口进 `Assets/Breakout/Scenes`，双击 `Breakout.unity`；
4. 按顶部 ▶ 播放键。

方式二（场景文件丢了 / 想从头来）：
1. 菜单栏 `打砖块` → `生成场景并保存（推荐）`；
2. 按 ▶ 播放。

方式三（最偷懒）：随便打开哪个场景（包括新建的空场景），直接按 ▶ —— 
`BreakoutAutoBoot.cs` 发现场景里没有 `GameManager` 会自动搭一局。

## 操作

| 按键 | 作用 |
| --- | --- |
| `A` / `D` 或 `←` / `→` | 左右移动挡板 |
| `空格` | 发球 |
| `P` | 暂停 / 继续 |
| `R` | 重新开始 |

规则：3 条命，球掉下去扣一条；把 40 块砖（8 列 × 5 行）全清掉就通关。
每打掉一块砖球速 +0.15（上限 18）。

## 脚本各管什么

| 文件 | 职责 |
| --- | --- |
| `Scripts/MaterialFactory.cs` | 渲染管线无关的材质工厂（URP / 内置管线都能跑） |
| `Scripts/InputHelper.cs` | 输入统一出口（新旧两套 Input 系统都能跑） |
| `Scripts/GameManager.cs` | 全局状态机：分数 / 命数 / 输赢 / 重开 |
| `Scripts/PaddleController.cs` | 挡板移动 |
| `Scripts/BallController.cs` | 球的速度、归一化、反弹角度 |
| `Scripts/Brick.cs` | 单块砖：加分 + 闪白 + 销毁 |
| `Scripts/BreakoutHud.cs` | IMGUI 画的 HUD（分数 / 生命 / 提示 / 飘字） |
| `Scripts/BreakoutBuilder.cs` | 用代码搭出整个场景 |
| `Scripts/BreakoutAutoBoot.cs` | 空场景按 Play 时的兜底自动搭场 |
| `Editor/BreakoutMenu.cs` | 菜单栏 `打砖块/…`（生成场景、自检等），只在编辑器里编译 |

## 设计上的坑，以及为什么这么写

- **为什么球是个 3D 刚体？** 因为 `Rigidbody` 上可以直接锁死 Y 轴和旋转
  （`RigidbodyConstraints.FreezePositionY | FreezeRotation`），再关掉重力，
  它就退化成一个"只能在平面上飞的 2D 球"，但享受到完整的连续碰撞检测，
  高速时不会穿过薄墙。
- **为什么每物理帧要重新归一化速度？** 物理引擎反复反射会累积误差，球会越弹越慢。
  另外速度太平的时候球会在左右墙之间无限横跳，所以强制掰一个角度出来。
- **为什么挡板用 `MovePosition` 而不是 `transform.position = ...`？**
  kinematic 刚体直接改 transform 会瞬移，物理引擎算不出正确的接触点，球会被"穿"过去。
- **为什么砖块共用材质、却用 `MaterialPropertyBlock` 改色？**
  40 块砖一个材质能少 40 个 SetPassCall；单独改色靠 property block，不影响别的砖。
- **为什么 HUD 用最老的 IMGUI？** uGUI 和 TextMeshPro 都得先在编辑器里搭 Canvas、
  做字体资产，而 `OnGUI` 一个脚本就能出字 —— 适合练手工程。
  中文靠 `Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 24)` 从系统字体里动态取。

## 想改点东西试试

| 想改什么 | 改哪儿 |
| --- | --- |
| 砖块行列数 | `BreakoutBuilder.Cols` / `Rows` |
| 球速、加速幅度 | `BallController.speed` / `speedUpPerBrick` / `maxSpeed`（或在 Inspector 里改） |
| 挡板速度、长度 | `PaddleController.speed` / `BreakoutBuilder.PaddleSizeX` |
| 命数 | `GameManager.startingLives` |
| 砖块分值 | `BreakoutBuilder.RowPoints` |
| 相机角度 | `BreakoutBuilder.SetupCamera()` |