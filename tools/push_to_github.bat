@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

rem ============================================================
rem  一键推送到 GitHub
rem  用法:
rem     push_to_github.bat https://github.com/benyshen/zebra-rfid-writer.git
rem     push_to_github.bat https://github.com/benyshen/zebra-rfid-writer.git http://127.0.0.1:10809
rem  第二个参数是本机代理（v2rayN 的 HTTP 端口通常是 10809；不填则直连/用系统代理）
rem ============================================================

set REPO_URL=%~1
set PROXY=%~2
set GIT="C:\Program Files\Git\cmd\git.exe"
set PROJ=%~dp0..

if "%REPO_URL%"=="" (
  echo [x] 缺少仓库地址。示例：push_to_github.bat https://github.com/benyshen/zebra-rfid-writer.git
  exit /b 1
)

cd /d "%PROJ%"

echo [1/4] 仓库状态
%GIT% status --short
%GIT% log --oneline -1

echo [2/4] 设置远端
%GIT% remote remove origin >nul 2>&1
%GIT% remote add origin "%REPO_URL%"

echo [3/4] 推送 main
if "%PROXY%"=="" (
  %GIT% -c http.proxy= -c https.proxy= push -u origin main
) else (
  %GIT% -c http.proxy=%PROXY% -c https.proxy=%PROXY% push -u origin main
)

if errorlevel 1 (
  echo.
  echo [x] 推送失败。常见原因：
  echo     - GitHub 连不通：先启动 v2rayN 核心并选中可用节点
  echo     - 仓库不存在：先在 GitHub 网页创建空仓库（不要勾选 README/gitignore）
  echo     - 凭据未保存：会弹窗登录，选 Windows 凭据管理器保存
  exit /b 1
)

echo.
echo [4/4] 完成：%REPO_URL%
%GIT% remote -v
endlocal
