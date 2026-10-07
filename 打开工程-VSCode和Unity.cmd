@echo off
chcp 65001 >nul
set "PROJ=%~dp0"
set "CODE=%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe"
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe"

echo 工程目录: %PROJ%

if not exist "%CODE%"  ( echo [错误] 找不到 VS Code: %CODE% & pause & exit /b 1 )
if not exist "%UNITY%" ( echo [错误] 找不到 Unity:   %UNITY% & pause & exit /b 1 )

echo 正在打开 VS Code ...
start "" "%CODE%" "%PROJ%"

echo 正在打开 Unity ...
start "" "%UNITY%" -projectPath "%PROJ%"

echo 完成。VS Code 里按 F5 附加调试前，记得先在 Unity 里按 ▶。
timeout /t 3 >nul
