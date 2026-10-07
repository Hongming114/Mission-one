# Unity 打砖块：跑起来 + Unity 入门 + C# 速成（写给会 C 的人）

> **工程位置**：`C:\Users\chair\Desktop\ds工作区\unity_breakout`
> **编辑器**：Unity 6000.5.2f1（Unity 6.5），URP 渲染管线，3D
> **配套**：另一份 `README.md` 在 `Assets/Breakout/README.md`，讲的是脚本各自干什么
> **阅读建议**：会 Unity 的话可以直接跳到第 4 章《C# 和 C 到底差在哪》。

---

## 0. 你这台机器的环境（我已经查过了）

### 已经装好、可以直接用

| 东西 | 版本 | 位置 |
| --- | --- | --- |
| Unity Hub | 3.19.3 | `C:\Program Files\Unity Hub\Unity Hub.exe` |
| Unity 编辑器 | **6000.5.2f1（= Unity 6.5）** | `C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe` |
| Unity 许可证 | **已激活**（走 IPC，登录状态在 `LicenseClient-chair`） | 不用再登录 |
| Visual Studio | Community 2026 18.9.2 | `C:\Program Files\Microsoft Visual Studio\18\Community` |
| .NET SDK | 10.0.400 / 10.0.401 | `C:\Program Files\dotnet` |

**结论：环境是完整的，不需要再装任何东西。**

### 三个小提醒

1. **只装了 Windows 打包模块**（`windowsstandalonesupport`）。想打包成安卓 / 网页版，
   要在 Unity Hub 里给 6000.5.2f1 这个版本 `Add modules` 勾上对应平台。
2. **中文路径**：你这台机器上工程就放在 `ds工作区`（中文目录）里，Unity 6 能正常跑。
   但**打包产物**和某些第三方插件偶尔会对中文路径犯病 —— 真出怪问题，
   先把工程挪到 `D:\unity_breakout` 这种纯英文路径再试。
3. **磁盘**：C 盘剩约 **50 GB**，D 盘剩约 **429 GB**。新工程建议放 D 盘。
   （PowerShell 的 `Get-PSDrive` 在这台机器上会报 C 盘剩余 0，那是沙箱造成的假象；
   要看真值用下面这条。）

```powershell
python -c "import shutil;u=shutil.disk_usage('C:\\');print('C: 剩 %.1f GB / 共 %.1f GB'%(u.free/2**30,u.total/2**30))"
```

---

## 1. 三步把游戏跑起来

### 第零步：先确认 Unity 有有效许可证（只做一次）

> **如果直接启动 `Unity.exe` 时弹出**
> `License error — No valid Unity Editor license found. Please activate your license.`
> **就是这一步没过，先按下面做。**

Unity 6 的授权是这样流转的：

```
Unity Hub（你登录的账号）
      |  下发登录令牌（token）
      v
Unity Licensing Client（后台服务）        日志：%LOCALAPPDATA%\Unity\Unity.Licensing.Client.log
      |  换成一张带"离线有效期"的授权文件
      v
%LOCALAPPDATA%\Unity\licenses\UnityEntitlementLicense.xml
      |  被 Editor 读取
      v
Unity Editor 能启动 ✔
```

**关键点**：那张授权文件不是永久的，它带一个**离线有效期**。
如果长时间不开 Hub（登录令牌过期），又不开编辑器，就会走到
"离线有效期已过 + 无法联网续期"的死角，于是 Editor 拒绝启动。

修复只有一条路 —— **重新用 Unity Hub 登录一次**：

1. 打开 Hub：`Start-Process "C:\Program Files\Unity Hub\Unity Hub.exe"`
2. 右上角头像 → `Sign in`，登录你当初用的 Unity ID
   （忘了密码去 <https://id.unity.com> 重置，个人版是免费的，不需要序列号）。
3. Hub 左侧 `Preferences`（齿轮）→ `Licenses`，应该能看到 **Unity Personal**；
   没有就点 `Add` → `Get a free personal license`。
4. **从 Hub 里打开工程**：`Projects` → `Add` → 选 `...\unity_breakout` → 点它启动。
   ⚠️ 登录修好之前，直接 `Start-Process Unity.exe -ArgumentList '-projectPath', ...` 仍然会被拒，
   因为本地那张授权文件已经失效，只有 Hub 能换一张新的回来。

> **怎么自己确认是这个毛病**：打开 `%LOCALAPPDATA%\Unity\Unity.Licensing.Client.log`，
> 看最后几行。如果有
> `Ignoring license: ... (LicenseGroupOfflineValidityPeriodIsExpired)` 和
> `Failed to update license file. [Code: 401] Token not found in cache`，
> 那就一定是它。

---

### 第一步：打开工程

> 前提：第零步的许可证已经好了。

在 PowerShell 里粘这一行（会启动 Unity；**第一次打开要等 1~3 分钟**导入资源 + 编译脚本）：

```powershell
Start-Process "C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe" `
  -ArgumentList '-projectPath', 'C:\Users\chair\Desktop\ds工作区\unity_breakout'
```

等什么：**等右下角的转圈图标消失**。转圈时脚本还在编译，这时候按 Play 会被拦下来并提示
`All compiler errors have to be fixed before you can enter Play Mode`。

也可以用 Hub 图形界面：打开 Unity Hub → `Projects` → `Add` → 选 `unity_breakout` 文件夹 → 点它。

### 第二步：打开场景（也可以直接跳过这步）

Unity 第一次打开这个工程时，默认已经载入了模板自带的场景 `Assets/Scenes/SampleScene.unity`
（`Library/LastSceneManagerSetup.txt` 里预先写好了）。它里面只有一台相机和一盏平行光 ——
**看着一片空空的，这是正常的，不是出错。**

接下来有两条路，选一条：

**路线 A（最省事，先走这条）**：什么都不用点，直接去第三步按 `▶`。
脚本 `BreakoutAutoBoot.cs` 发现当前场景里没有 `GameManager`，会**用代码把整局游戏搭出来**。

**路线 B（把场景实体化、存成文件）**：菜单栏 `打砖块` → `生成场景并保存（推荐）`。
它会生成 `Assets/Breakout/Scenes/Breakout.unity` 并自动加进 Build Settings。
之后在 `Project` 窗口里双击 `Assets/Breakout/Scenes/Breakout.unity`，
就能在 `Scene` 窗口里看到真实摆好的砖墙 / 挡板 / 球，
想用鼠标拖一拖、在 Inspector 里调一调，就用这条路线。

> `Project` 窗口里的路径是 `Assets` → `Breakout` → `Scenes`；
> 如果只看到一列文件夹，点 `Project` 窗口左上角的层级小图标切成两列视图。

### 第三步：按 Play

编辑器**正中间顶部**有一个 `▶` 三角按钮，点它。游戏在 `Game` 窗口里跑。

| 按键 | 作用 |
| --- | --- |
| `A` / `D` 或 `←` / `→` | 左右移动挡板 |
| `空格` | 发球 |
| `P` | 暂停 / 继续 |
| `R` | 重开一局 |

规则：**3 条命**，球掉出底部扣一条；把 **40 块砖**（8 列 × 5 行）全清掉就通关。
每打掉一块砖球速 +0.15，上限 18。

**最偷懒的玩法**：不用管上面第二步。随便新建一个空场景（`File` → `New Scene`），直接按 `▶`。
脚本 `BreakoutAutoBoot.cs` 发现场景里没有 `GameManager`，会自己用代码搭一局出来。

### 退出播放

**再点一次那个 `▶`**（播放时它变成蓝色高亮）。播放模式下你做的一切改动都不会被保存 ——
这是 Unity 新手最需要记住的一条：**Play 模式 ≈ 临时试玩，退出即还原**。

---

## 2. Unity 编辑器界面：六个窗口 + 一个工具栏

打开 Unity 你会看到一堆格子，其实就是这六块（都可以拖、可以关、关了从 `Window` 菜单找回来）：

| 窗口 | 它是什么 | 你现在会看到 |
| --- | --- | --- |
| **Hierarchy（层级）** | 当前场景里**有哪些物体**（一棵树） | `[打砖块]` 下面挂着 `Paddle` `Ball` `Bricks` `HUD` `Floor` `WallLeft`… |
| **Scene（场景）** | **3D 视图**，用来摆物体（鼠标右键旋转、滚轮缩放、中键平移） | 一个俯视的场地 |
| **Game（游戏）** | **摄像机看到的画面** —— 游戏真正长这样 | 就是你要玩的画面 |
| **Inspector（检视）** | 选中某个物体/资源后，**它的属性和组件** | 选中 `Ball` 能看到 `Rigidbody`、`Sphere Collider` 和脚本字段 |
| **Project（工程）** | `Assets` 文件夹里的**所有资源文件** | 脚本、场景、材质、图片都在这里 |
| **Console（控制台）** | **日志、警告、报错** | 启动时会有几行 `[打砖块] …` |

**工具栏**（最上面一行）从左到右：

- 最左边 `✋ / ✥ / ⟳ / ⤢` —— Scene 视图的操作模式（手型平移 / 移动物体 / 旋转 / 缩放）。
- 正中间 `▶ ⏸ ⏭` —— 播放 / 暂停 / 单步一帧。**这是你最常点的三个键。**
- 右边 `Pause` `Step` —— 暂停时用来一帧一帧看。

### 新手最该记住的四个快捷键

| 快捷键 | 作用 |
| --- | --- |
| `Ctrl + S` | **保存场景**（改了场景不保存，切场景就丢） |
| `Ctrl + Shift + S` | 另存场景 |
| `Ctrl + D` | 复制选中的物体 |
| `Delete` | 删除选中的物体 |

---

## 3. Unity 的核心心智模型（理解这 5 个词就能看懂 90% 的教程）

### 3.1 场景（Scene）= 一关/一个画面

一个 `.unity` 文件就是一个场景。里面装着一堆**游戏对象**。
「切关卡」「回主菜单」在 Unity 里就是**切换场景**。

### 3.2 游戏对象（GameObject）= 一个空的容器

`GameObject` 自己**什么都不做**，它是空壳子。真正让它有外表、有行为、有碰撞的，是挂在它上面的**组件**。

### 3.3 组件（Component）= 功能模块

你从来不是"继承一个 Player 类"，而是"往空物体上**拼装**组件"：

```
GameObject "Ball"
├── Transform            ← 每个 GameObject 都强制有一个：位置 / 旋转 / 缩放
├── Mesh Renderer        ← 让它能被看见（画一个球）
├── Sphere Collider      ← 让它参与碰撞检测
└── BallController.cs    ← 你自己写的脚本，也是组件
```

**这是 Unity 和「传统 C 写法」最大的思维差异**：不写 `ball_update(ball *b)` 这样的函数，
而是写一个继承自 `MonoBehaviour` 的类，挂到物体上，引擎每帧来调你的方法。
（这种思路叫**组合优于继承**，玩过 ECS 的话会觉得眼熟。）

**挂脚本的两种方式**：

- 编辑器里：把 `Assets` 里的 `.cs` 文件**拖到** Hierarchy 里的物体上；
- 代码里：`gameObject.AddComponent<BallController>()` —— 本工程就是用这种「纯代码搭场景」的路子。

### 3.4 预制体（Prefab）= 可复用的物体模板

把一个配好的物体从 Hierarchy 拖到 Project 窗口，就变成一个蓝色的 `.prefab` 资源。
以后拿它当模板批量生成（敌人、子弹、砖块都用它）。
本工程没用 Prefab —— 因为砖块是 `BreakoutBuilder.BuildBricks()` 用代码批量生成的，
改起来比拖 40 次更省事。

### 3.5 生命周期（Lifecycle）= 引擎什么时候调你的函数

这是**写 Unity 脚本最关键的一张表**，函数名必须拼写完全正确（引擎靠**反射**按名字找）：

| 函数 | 什么时候被调用 | 用来干什么 |
| --- | --- | --- |
| `Awake()` | 物体被**加载**时，只一次（比 Start 早） | 拿组件引用、初始化自己的状态 |
| `OnEnable()` | 每次**启用**时（可能多次） | 注册事件 |
| `Start()` | 第一次 `Update` **之前**，只一次 | 需要用到别人的 Awake 结果的初始化 |
| `Update()` | **每一帧**（跟帧率走，可能 144Hz） | 读输入、更新逻辑 |
| `FixedUpdate()` | **每 0.02 秒**固定一次（跟物理走） | 用 Rigidbody 做物理操作 |
| `LateUpdate()` | 所有 `Update` 都跑完之后 | 摄像机跟随（否则会抖） |
| `OnGUI()` | 每帧画 GUI 时（可能画多次） | 老式 HUD |
| `OnCollisionEnter(Collision c)` | 撞上另一个碰撞体时 | 碰撞响应 |
| `OnTriggerEnter(Collider c)` | 进入一个"触发器"时 | 判定区域 |
| `OnDestroy()` | 物体被销毁时 | 收尾、注销事件 |

一帧内部的顺序大致是：

```
所有脚本的 Update()   →  物理引擎（FixedUpdate、碰撞检测、碰撞回调）  →  所有 LateUpdate()  →  渲染  →  OnGUI
```

**为什么把球的速度限制放在 `FixedUpdate` 而不是 `Update`？**
物理引擎在 `FixedUpdate` 之后才积分位置。在 `Update` 里改速度，速度改了几次是随机的；
在 `FixedUpdate` 里改，每次物理步进前速度都是确定的，结果可复现。
**这是"物理要写 FixedUpdate、输入/动画写 Update"这条规矩的由来。**

### 3.6 三个"和普通 C# 不一样"的 Unity 特性，先记住结论

1. **`MonoBehaviour` 里的字段是"序列化"的**。
   `public float speed = 16f;` 这一行，会让 Unity 把 `speed` 存进场景文件，
   并在 Inspector 里显示成一个可编辑的输入框。
   **你以后在 Inspector 里把 speed 改成 20，代码里写的 `= 16f` 就不生效了**
   （场景里存的值会覆盖代码里的默认值）—— 想恢复默认要右键组件 → `Reset`。
   不想暴露给 Inspector 又想保存，用 `[SerializeField] private float speed = 16f;`。

2. **UnityEngine.Object 的 `== null` 被重载过**。
   销毁一个物体后，C# 引用还在，但你 `if (obj == null)` 会得到 `true`
   （这叫"假 null"）。所以**永远别用 `?.` 或 `is null` 去判 Unity 对象**，老老实实 `== null`。

3. **不要用 `new` 创建 MonoBehaviour**。
   `new BallController()` 在 Unity 里是错的（不会调用 `Awake`、没挂到物体上）。
   要创建只能 `AddComponent<T>()` 或者 `Instantiate(prefab)`。
---

## 4. C# 和 C 到底差在哪（写给会 C 的人）

下面全按"你已经在写 C"这个前提讲，只讲**你会踩的地方**，不讲教科书上的定义。

### 4.1 先记一张总表

| 维度 | C | C# |
| --- | --- | --- |
| 编译产物 | 机器码（直接给 CPU 跑） | **IL 中间语言**，运行时再 JIT 成机器码 |
| 运行时 | 几乎没有（只有 libc / crt） | 有 **CLR**：GC、反射、异常、类型信息 |
| 内存 | `malloc` / `free`，手动管 | `new`，**GC 自动回收**，你无法手动 free |
| 头文件 | `#include "xxx.h"` | **没有头文件**，`using 命名空间;` |
| 声明与定义 | 必须分离，要先声明后用 | 随便，**类的先后顺序无关**，跨文件互相调用也不用声明 |
| 入口 | `int main(int argc, char **argv)` | 库没有 main；Unity 里是引擎回调你的 `Start()` / `Update()` |
| 字符串 | `char *` / `char[]`，`strcmp` 比较，`'\0'` 结尾 | `string`，不可变，有 `.Length`，`==` 就是按内容比 |
| char | 1 字节 | **2 字节**（UTF-16） |
| 数组 | 退化成指针，**不知道自己有多长** | 有边界检查，`arr.Length` |
| 布尔 | C99 要 `<stdbool.h>` | 关键字 `bool` / `true` / `false` |
| 指针 | 核心机制 | 有，但要 `unsafe`，日常写 Unity 用不到 |
| 函数指针 | `void (*fp)(int)` | **委托** `delegate` / `Action<>` / `Func<>` |
| 结构体 | `struct` 只有数据 | `struct` 是**值类型**，`class` 是**引用类型**，还有 `interface` |
| 面向对象 | 没有，靠约定（`gtk_widget_show(p)`） | `class` / 继承 / `virtual` `override` / 属性 / 命名空间 |
| 泛型 | 没有，靠 `void*` + 宏 | `List<int>`、`Dictionary<string, int>` |
| 异常 | 没有（`setjmp/longjmp`） | `try` / `catch` / `finally`，**连空指针都能 catch** |
| 编译期宏 | `#define MAX 100` 真替文本 | **没有文本宏**，只有 `#if` 条件编译和 `const` / `static readonly` |
| 整数宽度 | `long` 在 Windows 上是 **32 位** | `int`32 / `long` **64** 位，宽度固定不随平台变 |
| 命名 | 随你，通常 `snake_case` | **类/方法 PascalCase，变量 camelCase**（Unity 官方风格） |

### 4.2 两个 hello world 的差别

**C：**

```c
#include <stdio.h>
int main(void) {
    printf("hello\n");
    return 0;
}
```

**C#（Unity）：**

```csharp
using UnityEngine;                  // 相当于"引入一堆符号"，但不是 #include，没有文本粘贴

public class Hello : MonoBehaviour  // 继承引擎的基类
{
    void Start()                    // 引擎第一次运行时回调，不是我调用的
    {
        Debug.Log("hello");         // 不是 printf，输出到 Unity 的 Console 窗口
    }
}
```

三个立刻能感觉到的差别：

1. **没有 `main`**。程序入口是 Unity 引擎自己，你写的类只是"被引擎回调的组件"。
2. **`using` 不等于 `#include`**。它是符号查找范围，不会把别的文件内容粘进来，
   也不会像 C 那样"同一个头文件被包含两次导致重复定义"。
3. **`Debug.Log` 不是 `printf`**。参数用 `+` 拼（或 `$"..."`），输出到 Console 窗口和日志文件。
   在编辑器里还能点日志跳到出错的那一行代码。

### 4.3 内存：`malloc/free` 换成了 `new` + GC

```c
/* C */
Ball *b = (Ball *)malloc(sizeof(Ball));
b->x = 1.0f;
...
free(b);
```

```csharp
/* C# */
Ball b = new Ball();   // 分配在"托管堆"上，交给 GC 管
b.x = 1.0f;
// 没有 free。b 没人引用了，GC 会在某个时刻自动回收
```

要点：

- **不能手动释放**。想"立刻释放"，只能断开所有引用然后等 GC。
- **值类型 vs 引用类型**——这是 C 里没有的概念，必须记住：

  | | 值类型（`struct` `int` `float` `bool` `Vector3` `Color`） | 引用类型（`class` `string` `int[]` `List<T>` `MonoBehaviour`） |
  | --- | --- | --- |
  | 存在哪 | 栈上 / 内联在对象里 | 托管堆上 |
  | `a = b` 的含义 | **拷贝一份** | **复制引用**（两个名字指向同一个对象） |
  | 传进函数 | 传副本，改不到外面 | 传引用，能改到里面的字段 |

  所以**在 Unity 里 `Vector3` 是 `struct`**：`Vector3 a = b;` 之后改 `a` 不会影响 `b`。
  这一点常让写 C 的人犯迷糊——C 里"结构体赋值也是拷贝"，跟这里其实是一样的，习惯就好。

- **装箱（boxing）**：把值类型塞进 `object` 时会在堆上新建一个盒子。
  `object o = 42;` 就装箱了一次。频繁装箱会产生 GC 垃圾。

- **Unity 的 GC 会掉帧**。Unity 用的是非分代 / 增量式的 GC，一次回收会卡住好几十毫秒。
  所以有经验的 Unity 程序员会避免在 `Update()` 里产生垃圾：

  | 这个操作 | 产生 GC 垃圾吗 |
  | --- | --- |
  | `new Vector3(1,2,3)` | ✅ 不产生（struct，在栈上） |
  | `transform.position = new Vector3(...)` | ✅ 不产生（属性里是 struct 拷贝） |
  | `"得分：" + score`（字符串拼接） | ❌ **产生**（每次拼出一个新 string） |
  | `new List<int>()` / `new string[]` | ❌ 产生 |
  | `GetComponent<Rigidbody>()` | ✅ 不产生（只在首次调用有点开销） |
  | `foreach (var x in someList)` （老版本 Mono） | 早期 Unity 的 `foreach` 会装箱枚举器，新版已经修好 |
  | lambda 捕获外部局部变量 | ❌ 会产生一个闭包对象 |

  怎么查：`Window` → `Analysis` → `Profiler`，选 `CPU`，看 `GC Alloc` 那一列。
  **本工程的 `BreakoutHud` 里 `hearts += ...` 每帧拼字符串 —— 这是一个故意的反面教材**，
  你要练手可以把生命值缓存成一个字符串，只在变化时重算。

- **`IDisposable` / `using` 语句**（注意和 `using UnityEngine;` 是两个不同的 `using`）：

  ```csharp
  using (var stream = new FileStream("a.txt", FileMode.Open))  // 作用域结束自动 Dispose
  {
      ...
  }
  ```
  Unity 里最常见的是 `using (UnityWebRequest req = ...)`。

### 4.4 类型系统上的具体差异

```csharp
// 1) 一切都是 object 的（直接或间接）派生
object o = 123;                 // 装箱
object s = "hi";                // string 本来就是引用类型
int n = (int)o;                 // 拆箱，要显式转

// 2) 数组知道自己多长，而且越界会抛异常（C 里是未定义行为）
int[] a = new int[10];
int len = a.Length;             // 10
// a[10] = 1;                   // 运行时报 IndexOutOfRangeException，不会静默踩内存

// 3) 字符串是引用类型但不可变，== 是按内容比的
string x = "abc";
string y = "a" + "bc";
bool same = (x == y);           // true —— C# 的 == 对 string 做了按内容比较
// 想做"是不是同一个对象"，要 object.ReferenceEquals(x, y)

// 4) 委托 ≈ 类型安全的函数指针
System.Action<int> f = (v) => Debug.Log(v);
f(3);

// 5) 泛型
List<int> list = new List<int>();     // 对比 C 里的"用 void* + 手写长度"
list.Add(1);

// 6) 可空类型（C 里想表达"没有值"只能约定用 -1 当哨兵）
int? maybe = null;
if (maybe.HasValue) { int v = maybe.Value; }

// 7) 属性（C 里要写 get_x()/set_x() 两个函数）
private int _lives = 3;
public int Lives
{
    get { return _lives; }
    set { _lives = Mathf.Max(0, value); }     // value 是关键字，代表传进来的值
}
// 简写：public int Lives { get; set; }   —— 只有 getter/setter 时才需要显式字段
```

### 4.5 C 里没有的"面向对象"部分

```csharp
public class Animal                      // 类
{
    protected int age;                   // 访问修饰符默认是 private（C 里默认是全局可见）
    public Animal(int age) { this.age = age; }   // this 可以省，习惯上不省

    public virtual string Speak() { return "..."; }   // virtual = 允许子类改写
}

public sealed class Dog : Animal         // 继承；sealed = 不许再被继承
{
    public Dog(int age) : base(age) { }  // 调父类构造函数
    public override string Speak() { return "汪汪"; }  // 必须写 override，写错编译器会报
}

public interface IDamageable             // 接口：只有签名没有实现（C 里用函数指针表模拟）
{
    void TakeDamage(int amount);
}
```

几个和 C 的思维差异：

- **访问修饰符默认 `private`**。C 里不写 `static` 就是"文件外也能用"，
  C# 里不写就是"只有自己这个类能用"。
- **`virtual` 和 `override` 必须成对出现**。忘了 `override` 会变成"隐藏"，编译器给警告 —— 这点比 C++ 严格，是好事。
- **C# 没有头文件，所以没有"未声明标识符"这个错**。想调用别的文件里的类，
  只要在同一个命名空间（或 `using` 了它的命名空间）就能直接用。
- **`namespace` 比 C 的命名约定更硬**。C 里靠 `gtk_widget_show()` 这种前缀避免重名，
  C# 里编译器真的会把不同命名空间里的同名类区分开。
  本工程所有脚本都写在 `namespace Breakout { }` 里，编辑器脚本写在 `namespace Breakout.EditorTools { }` 里。

### 4.6 常见写法速查表

| 想干的事 | C 写法 | C# 写法 |
| --- | --- | --- |
| 定义常量 | `#define MAX 10` / `const int MAX = 10;` | `const int Max = 10;` 或 `static readonly int Max = 10;` |
| 打印调试 | `printf("%d\n", x);` | `Debug.Log(x);` / `Debug.Log($"x={x}")` |
| 格式化字符串 | `sprintf(buf, "%d", x);` | `$"x={x}"` 或 `string.Format("x={0}", x)` |
| 求长度 | `sizeof(arr)/sizeof(arr[0])` | `arr.Length` |
| 动态数组 | 手写 realloc | `List<int> list = new List<int>();` |
| 字符串长度 | `strlen(s)` | `s.Length` |
| 字符串比较 | `strcmp(a, b) == 0` | `a == b` |
| 复制字符串 | `strcpy(dst, src)` | `string d = s;`（赋值就够） |
| 拼接 | `strcat(a, b)` | `a + b` 或 `$"{a}{b}"` |
| 取绝对值 | `abs(x)` / `fabsf(x)` | `Mathf.Abs(x)` |
| 三角函数 | `sinf(x)` | `Mathf.Sin(x)` |
| 限制范围 | 手写 `if (x < lo) x = lo;` | `Mathf.Clamp(x, lo, hi)` |
| 线性插值 | 手写 | `Mathf.Lerp(a, b, t)` |
| 随机数 | `rand() % n` | `Random.Range(0, n)`（Unity 的，注意 **int 版是左闭右开**） |
| 回调 | `void (*cb)(int)` | `Action<int>` / `delegate` / `event` |
| 空值判断 | `if (p == NULL)` | `if (obj == null)`（Unity 对象要小心"假 null"） |
| 类型转换 | `(int)f` | `(int)f`，或 `f as T` / `Convert.ToInt32` |
| 断言 | `assert(x);` | `Debug.Assert(x);` |
| 遍历数组 | `for (i=0;i<n;i++)` | `foreach (var x in arr)` |
| 位运算 | 一样 | 一样 |

### 4.7 最容易踩的 12 个坑

1. **`switch` 不穿透**。C# 里 `case` 结束后必须有 `break` / `return` / `goto case`，
   否则**编译不过**。C 里忘写 `break` 是经典 bug，C# 用编译错误帮你挡住了。
2. **整数除法还是整数除法**。`1 / 2 == 0`，这点和 C 一模一样。
   想要小数至少一边是浮点：`1 / 2f`。
3. **`float` 要写 `1.5f`**。`1.5` 是 `double`，赋给 `float` 要显式转；这在 Unity 里天天遇到
   （`transform.position.x = 1.5;` 会报错，得写 `1.5f`）。
4. **`long` 是 64 位**。Windows 上 C 的 `long` 是 32 位，从 C 过来很容易搞错。C# 里 `int` 恒 32 位、`long` 恒 64 位。
5. **`char` 是 2 字节**。别拿它当"一个字节"用；要字节用 `byte`。
6. **`struct` 赋值是拷贝**。`Vector3 a = b; a.x = 9;` 之后 `b.x` 还是原值。
7. **不能改返回值里的字段**。`transform.position.x = 5;` **编译不过**，
   因为 `position` 是属性、`Vector3` 是 struct，返回的是一个临时副本。
   必须写成 `Vector3 p = transform.position; p.x = 5; transform.position = p;`
   —— 这条是 Unity 新手最容易撞的墙，**现在就有印象会省你很多时间**。
8. **`Mathf` 和 `Math` 是两个东西**。`Mathf` 是 Unity 的（float 版，有 `Clamp`/`Lerp`），
   `Math` 是 .NET 的（double 版，没有 `Lerp`）。Unity 里优先 `Mathf`。
9. **空引用异常 = 可捕获的段错误**。`NullReferenceException` 在 C# 里是普通异常，
   能 `try` 住；但**它通常说明你逻辑写错了**，不要靠 catch 掩盖。
10. **别用 `?.` 和 `is null` 判 Unity 对象**。`UnityEngine.Object` 重载了 `==`，
   销毁后的对象 `== null` 是 `true`，但 `is null` 是 `false`。用 `if (obj == null)`。
11. **`string` 拼接在热路径上是垃圾制造机**。`Update()` 里别拼字符串。
12. **`using` 有两个含义**：`using UnityEngine;`（引入命名空间）
    和 `using (var x = ...) { }`（自动调用 `Dispose`）。看上下文别搞混。

### 4.8 一个逐行对照的小例子：让一个物体每帧往前挪

**C**（假设你自己有个小引擎）：

```c
typedef struct { float x, y, z; } Vec3;

typedef struct {
    Vec3  pos;
    float speed;
} Object;

/* 引擎主循环负责调用这个函数，所以必须把对象和 dt 都传进来 */
void object_update(Object *o, float dt)
{
    o->pos.x += o->speed * dt;
}
```

**C#（Unity）**：

```csharp
using UnityEngine;

public class Mover : MonoBehaviour
{
    public float speed = 3f;          // public → 会在 Inspector 里出现一个输入框

    void Update()                     // 引擎每帧自己找到并调用它，不用我注册
    {
        transform.position += new Vector3(speed * Time.deltaTime, 0f, 0f);
    }
}
```

逐条解释差别：

| C | C# / Unity | 说明 |
| --- | --- | --- |
| 要传 `Object *o` | 不用传 | `this` 就是挂这个脚本的物体 |
| 要传 `float dt` | `Time.deltaTime` | 全局可取；`deltaTime` 是"上一帧花了多少秒" |
| 自己 `typedef struct Vec3` | 引擎给了 `Vector3` | 还有 `Vector2` `Quaternion` `Color` `Rect`… |
| `o->pos` | `transform.position` | `transform` 是每个 GameObject 强制自带的组件 |
| 引擎要显式调用 `object_update` | 引擎靠**反射**按名字找 `Update()` | 所以函数名拼错了不会报错，只是**永远不会被调用** ← 新手噩梦 |

**最后那条要划重点**：Unity 里 `void Updat()` 少打一个 `e`，编译器不会报错，
你的代码就像没写一样。遇到"我的代码怎么没执行"先检查函数名拼写。
---

## 5. 这个打砖块工程是怎么写出来的

### 5.1 文件清单

```
unity_breakout/                         ← Unity 工程根目录
├── Assets/
│   └── Breakout/
│       ├── README.md                   ← 脚本职责速查
│       ├── Scripts/
│       │   ├── MaterialFactory.cs      ← 渲染管线无关的材质工厂
│       │   ├── InputHelper.cs          ← 输入统一出口（新旧 Input 系统都能跑）
│       │   ├── GameManager.cs          ← 全局状态机：分数/命数/输赢/重开
│       │   ├── PaddleController.cs     ← 挡板
│       │   ├── BallController.cs       ← 球
│       │   ├── Brick.cs                ← 单块砖
│       │   ├── BreakoutHud.cs          ← HUD（IMGUI 画的）
│       │   ├── BreakoutBuilder.cs      ← 用代码搭出整个场景
│       │   └── BreakoutAutoBoot.cs     ← 空场景按 Play 时的兜底
│       └── Editor/
│           └── BreakoutMenu.cs         ← 菜单栏「打砖块/…」，只在编辑器里编译
├── Packages/manifest.json              ← 装了哪些包
├── ProjectSettings/                    ← 工程设置（画面尺寸、输入系统、物理…）
├── Library/                            ← 缓存的导入结果，**可以删，删了重新导入**
└── Unity打砖块-使用与Csharp入门.md      ← 就是这份文档
```

`Assets` 是你唯一该手动往里放东西的地方。`Library` 是缓存，`Temp` 和 `obj` 也是，
这三个都可以随时删掉让 Unity 重建（删 `Library` 的代价是下次打开要重新导入几分钟）。

### 5.2 三个值得学的设计决定

**① 场景是"代码搭出来的"，不是"手拖出来的"。**

`BreakoutBuilder.Build()` 把相机、灯光、地板、三面墙、挡板、球、40 块砖全部 `new` 出来。
好处：

- 你在编辑器里手滑删了东西，跑一次 `打砖块/生成场景并保存` 就恢复了；
- 场景的"真相"写在代码里，能 diff、能 review、能复制到别的工程；
- 想改布局改常量就行，不用在 3D 视图里对着拖。

代价：在编辑器里随手调的手感（比如把球速从 9 拖到 12）**不会被保存回代码**，
下次重新生成就还原了。所以本工程的做法是：
**能用代码定死的都定死，想临时调的用 Inspector 调、调好了再抄回代码**。

**② 球是三维刚体，但被锁成了一架"平面上的飞机"。**

```csharp
_rb.useGravity = false;                                                   // 关重力
_rb.constraints = RigidbodyConstraints.FreezePositionY                    // 锁 Y
               | RigidbodyConstraints.FreezeRotation;                     // 锁旋转
```

这样它其实退化成了 2D 球，但**白拿了物理引擎的连续碰撞检测**（`ContinuousDynamic`）——
球速再快也不会穿过 0.8 单位厚的墙。如果自己写 2D 圆形碰撞，高速穿墙是必然要处理的麻烦事。

**③ `MaterialFactory` 是为了"不管哪种渲染管线都能跑"。**

Unity 有两套常用管线：内置管线（Built-in）和 URP。它们**材质不通用** ——
URP 工程里 `Shader.Find("Standard")` 返回 `null`，内置管线工程里没有 URP 的 Lit。
所以 `MaterialFactory.BaseShader` 是一个"从上到下挨个试"的属性。
还有个副产品：`Shader.Find` 很慢，所以结果缓存在 `_cached` 里只找一次。

### 5.3 三段关键代码走读

**球的恒定速度**（`BallController.FixedUpdate`）：

```csharp
Vector3 v = _rb.linearVelocity;
v.y = 0f;
if (v.sqrMagnitude < 0.0001f) v = Vector3.forward * speed;
v = v.normalized * speed;                 // ← 每物理帧都把长度掰回 speed

// 角度太平的话球会在左右墙之间来回横跳半天，硬掰一点出来
if (v.z > -speed * 0.25f && v.z < speed * 0.25f)
{
    v.z = (v.z >= 0f ? 1f : -1f) * speed * 0.35f;
    v = v.normalized * speed;
}
_rb.linearVelocity = v;
```

- 物理引擎反复反射会累积浮点误差，球会越弹越慢或越弹越快 → 归一化解决。
- 打到左右墙之后 z 分量可能变成接近 0 → 球水平横跳 → 强制给个最小角度。
- **为什么在 `FixedUpdate` 而不是 `Update`？** 见 3.5 节。

**挡板的速度**（`PaddleController.FixedUpdate`）：

```csharp
Vector3 p = _rb.position;
p.x = Mathf.Clamp(p.x + _dir * speed * Time.fixedDeltaTime, minX, maxX);
_rb.MovePosition(p);      // ← 不是 transform.position = p
```

用 `MovePosition` 而不是直接改 `transform.position`，是因为物理引擎需要知道
"这个物体是**移动**过来的"才能算对接触点；直接瞬移会让球从挡板里穿过去。
（`FixedUpdate` 里必须用 `Time.fixedDeltaTime`，不是 `Time.deltaTime`。）

**反弹角度**（`BallController.OnCollisionEnter`）：

```csharp
float halfW = Mathf.Max(0.1f, paddle.transform.lossyScale.x * 0.5f);
float t = Mathf.Clamp((transform.position.x - paddle.transform.position.x) / halfW, -1f, 1f);
_rb.linearVelocity = (Quaternion.Euler(0f, t * 60f, 0f) * Vector3.forward).normalized * speed;
```

打在挡板正中间 → `t = 0` → 垂直弹回（往 +z）；
打在边缘 → `t = ±1` → 斜 60° 弹出去。
**这就是"手感"的全部来源**：玩家能通过击球位置控制球的去向。
（`Quaternion.Euler` 是绕 Y 轴旋转一个角度，`Vector3.forward` 是 (0,0,1)。）

---

## 6. 动手改一改（8 个练习，由易到难）

改完按 `Ctrl+S`，然后按 `▶` 看效果。改错了不要紧：`Edit` → `Undo`（`Ctrl+Z`），
或者把改动抄回来重来。

| # | 想做的事 | 改哪儿 | 提示 |
| --- | --- | --- | --- |
| 1 | 球跑快一点 | `BallController.speed`（9 → 14） | 或者**不改代码**，在 Hierarchy 选中 `Ball`，Inspector 里改 —— 这就是"序列化字段"的用处 |
| 2 | 命数改成 5 条 | `GameManager.startingLives`（3 → 5） | 注意 HUD 是按 `startingLives` 画心形的，会自动变成 5 个 |
| 3 | 砖块多两排 | `BreakoutBuilder.Rows`（5 → 7） | 同时要给 `RowColors` 和 `RowPoints` 补两项，否则会下标越界 |
| 4 | 挡板变宽 | `BreakoutBuilder.PaddleSizeX`（2.8 → 4） | 记得跟着改 `PaddleController.minX/maxX`（那是按 `HalfWidth` 算的，会自动跟着 `PaddleSizeX` 走 —— 看一眼 `BuildPaddle` 里怎么算的） |
| 5 | 加个"砖块要打两下才碎" | `Brick` 类 | 加 `public int hitPoints = 2;`，`OnHit()` 里先 `hitPoints--`，`<= 0` 才 `Destroy` |
| 6 | 球掉下去不扣命，只重置 | `GameManager.OnBallLost()` | 把 `lives--` 那段删掉，只留 `ResetBall()` |
| 7 | 打碎砖块时放个方块掉下来 | 新建脚本 + `OnBrickDestroyed` | 学 `GameObject.CreatePrimitive`、`AddComponent<Rigidbody>()`、加个 `OnCollisionEnter` 判定 |
| 8 | 加个"奖励砖"：打中挡板变长 5 秒 | 多处 | 经典进阶题：要在 `PaddleController` 里加协程（`IEnumerator` + `StartCoroutine`）做倒计时 |

---

## 7. 打包成 exe

### 方式一：点界面（简单）

1. 先**退出播放模式**，保存场景（`Ctrl+S`）。
2. 菜单 `File` → `Build Profiles`（老版 Unity 叫 `Build Settings`）。
3. 左边选 `Windows`，中间 `Scene List` 里确认有 `Assets/Breakout/Scenes/Breakout.unity` 并且打了勾
   （菜单 `打砖块/加入 Build Settings` 也能加）。
4. 点 `Build`，选个输出目录，等几分钟。产物是一个文件夹，**双击里面的 `.exe` 就能玩**。

### 方式二：命令行（我给你的 PowerShell）

**先把 Unity 编辑器关掉**（同一时间只能有一个进程持有这个工程）。

```powershell
$U = "C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe"
$P = "C:\Users\chair\Desktop\ds工作区\unity_breakout"

& $U -quit -batchmode -nographics -projectPath $P `
   -executeMethod Breakout.EditorTools.BreakoutMenu.CliBuildWindows `
   -logFile "$P\build.log"

Write-Host "退出码 = $LASTEXITCODE"
Get-Content "$P\build.log" -Tail 30
```

产物在 `unity_breakout\Build\Windows\Breakout3D.exe`。

- `-batchmode` = 不开界面；`-nographics` = 不用显卡；`-quit` = 干完就退出。
- `-executeMethod` 指定要运行的静态方法（就是 `BreakoutMenu.CliBuildWindows`，我在代码里给你留好了）。
- `-logFile` 把日志写到文件 —— 批量模式下屏幕上什么都看不到，**必须看日志**。
- 如果报 "Target platform not installed"，就是没装 Windows 打包模块；
  打开 Unity Hub → `Installs` → 6000.5.2f1 右边的齿轮 → `Add modules` → 勾 `Windows Build Support (IL2CPP)`。

---

## 8. 报错对照表（你会遇到的那些）

| 现象 / 报错 | 原因 | 怎么修 |
| --- | --- | --- |
| `License error — No valid Unity Editor license found. Please activate your license.` | 授权文件的**离线有效期**过了，且 Hub 的登录令牌也过期了，没法自动续期 | 见第 1 章「**第零步**」：打开 Unity Hub 重新登录 → `Preferences`/`Licenses` 确认有 Unity Personal → **从 Hub 里**打开工程（直接启动 Unity.exe 会一直被拒） |
| `All compiler errors have to be fixed before you can enter Play Mode` | 有脚本编译不过 | 打开 `Console` 窗口看红色条目，双击跳转到出错行。**改完等右下角转圈结束再按 Play** |
| 物体是**亮粉色 / 品红色** | 材质的 shader 找不到（通常是"URP 工程里用了内置管线材质"） | 本工程用 `MaterialFactory` 自动选管线，所以不该出现；出现了说明你自己 `new Material(...)` 了 |
| `InvalidOperationException: You are trying to read Input using the UnityEngine.Input class, but you have switched active Input handling to Input System package` | 工程设置成"只用新版 Input System"，但代码写的是 `Input.GetKey` | `Edit` → `Project Settings` → `Player` → `Other Settings` → `Active Input Handling` 改成 **`Both`**（本工程出场就设成 Both 了） |
| `NullReferenceException: Object reference not set to an instance of an object` | 用了一个 `null` 的引用 | Console 里双击这行，它会指向出错的**代码行**；最常见的是 Inspector 里的引用字段没拖 |
| `MissingReferenceException: The object of type 'GameObject' has been destroyed but you are still trying to access it` | 访问了已经被销毁的物体 | 销毁前先判 `if (obj != null)`；或用 `Destroy(obj, 2f)` 的延迟销毁时注意时机 |
| `The type or namespace name 'X' could not be found` | 类不在当前命名空间 / 拼写错 / 脚本没编译 | 检查 `namespace` 和拼写；确认文件在 `Assets` 目录下 |
| `error CS0246: 未能找到类型或命名空间名"UnityEditor"` | 在**运行时**脚本里 `using UnityEditor` | `UnityEditor` 只能被放进叫 `Editor` 的文件夹的脚本使用（本工程的 `Assets/Breakout/Editor/`） |
| 中文显示成**方块**或问号 | 字体不支持中文 | `BreakoutHud` 会自动挑 `微软雅黑`；如果系统里没有，Console 会有警告 |
| 我的代码**完全没执行** | 函数名拼错了（`Update` 写成 `Updat`），引擎反射找不到 | 逐字核对 `Awake` `Start` `Update` `FixedUpdate` `OnCollisionEnter` |
| 一直转圈，按 Play 没反应 | 后台在导入资源 / 编译脚本 | 等右下角转圈结束。第一次打开工程要 1~3 分钟 |
| 场景打开是**空的 / 一片灰** | 没打开 `Breakout.unity`，停在默认空场景 | `Assets/Breakout/Scenes` 里双击 `Breakout.unity`；或者直接按 `▶` 让 AutoBoot 兜底 |
| 改了代码但行为没变 | 还在播放模式里 | **退出播放模式**再看；播放模式里的改动不会被保存 |
| 报错 `Target platform not installed` | 缺打包模块 | Unity Hub → Installs → Add modules |
| 编辑器卡死 / 提示工程被锁 | 开了两个 Unity 实例指向同一个工程 | 关掉多余的 Unity 进程 |

**看日志的位置**（Unity 关了也能翻）：
`%LOCALAPPDATA%\Unity\Editor\Editor.log`

```powershell
Get-Content "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Tail 60
```

---

## 9. 接下来学什么

按这个顺序走，每一步都有立刻能看到的东西：

1. **`Transform` + `GameObject` + `Instantiate`** —— 能做出"点击发射子弹"就已经跨过门槛了。
2. **`Rigidbody` 和碰撞** —— 分清 `Collider`（形状）和 `Rigidbody`（物理体）；
   分清 `OnCollisionEnter`（实体碰撞）和 `OnTriggerEnter`（穿过但不阻挡）。
3. **`Coroutine`（协程）** —— `IEnumerator` + `yield return new WaitForSeconds(1f)`，
   用来做"3 秒后爆炸""无敌 1.5 秒"这类定时逻辑。比手写计时器舒服得多。
4. **`Input` / `InputSystem`** —— 键盘鼠标手柄、输入映射。
5. **`ScriptableObject`** —— 用来存"配置/数据表"（武器属性、关卡配置），
   比 `ScriptableObject` 更适合替代全局单例和 JSON 配置。
6. **UI** —— 旧的 `uGUI`（Canvas + Text/Image/Button）或新的 `UI Toolkit`。
   本工程因为要"一个脚本就能跑"才用了最老的 IMGUI，**正经项目别学这个**。
7. **动画 `Animator`**、**粒子 `ParticleSystem`**、**音频 `AudioSource`**。
8. **场景管理与存档** —— `SceneManager.LoadScene`、`PlayerPrefs`、`JsonUtility`。
9. **打包与性能** —— `Profiler`、`Frame Debugger`、`Addressables`。

**官方资源（都免费，中文也有）：**

- Unity 手册（查功能）：<https://docs.unity3d.com/cn/current/Manual/index.html>
- Unity 脚本 API（查类和方法，**最常开的一个页面**）：<https://docs.unity3d.com/cn/current/ScriptReference/index.html>
- Unity Learn（跟着做项目）：<https://learn.unity.com/>
- C# 语言文档：<https://learn.microsoft.com/zh-cn/dotnet/csharp/>

**查 API 的正确姿势**：不确定某个类有没有某个方法时，在搜索引擎里打
`Unity 类名 方法名`，第一条基本就是官方文档；或者直接在 Visual Studio 里
把光标放在类型上按 `F12` 跳转到定义。

---

## 附录 A：常用 PowerShell 命令清单

```powershell
# ── 1. 打开这个工程（首次打开要等 1~3 分钟导入）
Start-Process "C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe" `
  -ArgumentList '-projectPath', 'C:\Users\chair\Desktop\ds工作区\unity_breakout'

# ── 2. 打开 Unity Hub（图形界面装模块 / 建新工程）
Start-Process "C:\Program Files\Unity Hub\Unity Hub.exe"

# ── 3. 看看装了哪些 Unity 版本和模块
Get-ChildItem "C:\Program Files\Unity\Hub\Editor" | Select-Object Name
Get-ChildItem "C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Data\PlaybackEngines" | Select-Object Name

# ── 4. 看编辑器日志（最后 60 行）
Get-Content "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Tail 60

# ── 5. 命令行打包成 exe（必须先关掉 Unity 编辑器）
$U = "C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe"
$P = "C:\Users\chair\Desktop\ds工作区\unity_breakout"
& $U -quit -batchmode -nographics -projectPath $P `
   -executeMethod Breakout.EditorTools.BreakoutMenu.CliBuildWindows `
   -logFile "$P\build.log"
Get-Content "$P\build.log" -Tail 30

# ── 6. 命令行新建一个空工程（想从零开始练的时候用；同样要先关掉 Unity）
& $U -batchmode -createProject "D:\MyFirstUnityGame" -quit -logFile "$env:TEMP\newproj.log"

# ── 7. 磁盘真实剩余空间（PowerShell 自带的在这个沙箱里会报 0）
python -c "import shutil;u=shutil.disk_usage('C:\\');print('C: 剩 %.1f GB / 共 %.1f GB'%(u.free/2**30,u.total/2**30))"
```

> **命令行最常见的两个坑**
> 1. 工程被 Unity 编辑器占用时，`-batchmode` 命令会直接失败（日志里能看到 "lock" 或干脆没反应）——
>    **先关掉编辑器**。
> 2. `-batchmode` 下屏幕上看不到任何输出，**必须加 `-logFile`**，否则你不知道它在干什么。

---

## 附录 B：这个工程我做了哪些验证

我不是在 Unity 里点出来的，所以额外做了这些检查：

1. **用 Unity 自带的 Roslyn 编译器，脱离 Unity 把 10 个 .cs 全部编译了一遍**，三种输入系统配置下都是
   **0 error / 0 warning**：
   - 只开旧版 Input Manager
   - 只开新版 Input System
   - 两者都开（本工程的设置）
   命令大致是这样（感兴趣可以自己跑）：

   ```powershell
   $E = "C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Data"
   $CSC = "C:\Program Files\dotnet\sdk\10.0.400\Roslyn\bincore\csc.dll"
   # 引用 Unity 的 UnityEngine / UnityEditor 程序集 + netstandard2.1 参考程序集，
   # 然后 dotnet exec csc.dll -target:library -nostdlib+ -r:... <所有 .cs>
   ```

2. **核对了所有用到的 Unity API 在这个版本里存在**（直接扫 `UnityEngine.*.dll`）：
   `PhysicsMaterial`、`Rigidbody.linearVelocity` / `linearDamping`、`FindAnyObjectByType`、
   `Font.CreateDynamicFontFromOSFont` / `GetOSInstalledFontNames`、`RuntimeInitializeOnLoadMethod`、
   `BuildPipeline.BuildPlayer` / `BuildReport` 都在。
   （顺带修掉了 Unity 6.5 里已经过时的 `FindFirstObjectByType`，换成了 `FindAnyObjectByType`。）
3. **工程模板来自 Unity 官方**（`com.unity.template.3d-cross-platform-17.0.14`），
   包版本对齐了你机器上另一个已经成功打开过的工程，避免版本不匹配。
4. **`activeInputHandler` 我改成了 `2`（Both）**，所以 `InputHelper` 走的是兼容性最好的那条分支。

唯一没法替你做的：**按下 ▶ 真正跑一遍**（我这边启动不了 Unity 进程，没有权限）。
如果 `▶` 之后有报错，把 Console 里的红字发我，我按行号改。
