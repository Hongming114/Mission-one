<#
    打砖块 —— 命令行打包（全程不打开 Unity 界面）

    用法：
        .\.vscode\build-breakout.ps1                    # 发布版 -> Build\Windows\Breakout3D.exe
        .\.vscode\build-breakout.ps1 -Mode Debug        # 可调试版 -> Breakout3D-Debug.exe
        .\.vscode\build-breakout.ps1 -Mode Debug -Run   # 打包完直接启动

    注意：打包期间 Unity 编辑器必须完全关闭（一个工程只能被一个 Unity 实例占用）。
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')] [string] $Mode = 'Release',
    [switch] $Run
)

$ErrorActionPreference = 'Stop'

$projDir  = Split-Path -Parent $PSScriptRoot          # .vscode 的上一级就是工程根目录
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe'
$exeName  = if ($Mode -eq 'Debug') { 'Breakout3D-Debug.exe' } else { 'Breakout3D.exe' }
$exePath  = Join-Path $projDir "Build\Windows\$exeName"
$logPath  = Join-Path $projDir 'build.log'

Write-Host "=== 打砖块命令行打包 ===" -ForegroundColor Cyan
Write-Host "  工程    : $projDir"
Write-Host "  模式    : $Mode"
Write-Host "  产物    : $exePath"
Write-Host ""

if (-not (Test-Path $unityExe)) { throw "找不到 Unity：$unityExe" }

# --- 前提 1：不能有 Unity 实例占着工程 ---
$running = @(Get-Process -Name Unity -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Write-Host "[中断] 检测到 Unity 编辑器正在运行（PID: $($running.Id -join ', ')）。" -ForegroundColor Red
    Write-Host "       命令行打包要求工程没有被占用，请先完全退出 Unity 再重试。" -ForegroundColor Red
    exit 1
}

# --- 前提 2：清掉上次异常退出留下的锁文件 ---
$lock = Join-Path $projDir 'Temp\UnityLockfile'
if (Test-Path $lock) {
    Remove-Item $lock -Force -ErrorAction SilentlyContinue
    Write-Host "[清理] 删除了残留的 Temp\UnityLockfile" -ForegroundColor Yellow
}

# --- 跑 Unity 命令行打包 ---
$unityArgs = @(
    '-quit', '-batchmode', '-nographics',
    '-projectPath',    $projDir,
    '-executeMethod',  'Breakout.EditorTools.BreakoutMenu.CliBuildWindows',
    '-logFile',        $logPath
)
if ($Mode -eq 'Debug') { $unityArgs += '-breakoutDebug' }

Write-Host "[打包] 正在调用 Unity（无界面，第一次会比较慢，请耐心等）..." -ForegroundColor Cyan
$sw = [Diagnostics.Stopwatch]::StartNew()
& $unityExe @unityArgs
$code = $LASTEXITCODE
$sw.Stop()
$secs = [int]$sw.Elapsed.TotalSeconds

# --- 判定成败 ---
if ($code -ne 0 -or -not (Test-Path $exePath)) {
    Write-Host ""
    Write-Host "[失败] Unity 退出码 = $code，耗时 $secs 秒" -ForegroundColor Red
    if (Test-Path $logPath) {
        Write-Host "--- build.log 最后 40 行 ---" -ForegroundColor Yellow
        Get-Content $logPath -Tail 40 -Encoding UTF8 | ForEach-Object { Write-Host "  $_" }
        Write-Host "--- 完整日志：$logPath ---" -ForegroundColor Yellow
    }
    exit 1
}

$mb = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
Write-Host ""
Write-Host "[成功] $exePath  （exe 本体 $mb MB，打包耗时 $secs 秒）" -ForegroundColor Green

# --- 可选：直接运行 ---
if ($Run) {
    Write-Host ""
    Write-Host "[运行] 启动 $exePath" -ForegroundColor Cyan
    Start-Process -FilePath $exePath
    if ($Mode -eq 'Debug') {
        Write-Host ""
        Write-Host "想断点调试：回 VS Code 按 Ctrl+Shift+P，输入 Attach Unity Debugger，" -ForegroundColor Cyan
        Write-Host "            在列表里选刚启动的这个播放器，然后打开 .cs 下断点。" -ForegroundColor Cyan
    }
}

exit 0