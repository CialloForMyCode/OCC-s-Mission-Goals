@echo off
setlocal
REM ============================================================
REM  OCC's Mission & Goals — 一键构建 Releases 安装包
REM
REM  用法：双击本文件，或在仓库根目录执行 installer\build-installer.cmd
REM  产物：<仓库根>\output\OCC-Mission-Goals-<版本>-x64-setup.exe
REM                            OCC-Mission-Goals-<版本>-x86-setup.exe
REM ============================================================

set "ROOT=%~dp0.."
set "INNO=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
set "PROJ=%ROOT%\OCC's Mission & Goals.csproj"

if not exist "%INNO%" (
    echo [错误] 找不到 Inno Setup 编译器: %INNO%
    exit /b 1
)

echo [1/4] 发布 win-x64 自包含单文件...
dotnet publish "%PROJ%" -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=None -p:DebugSymbols=false -o "%ROOT%\publish\win-x64"
if errorlevel 1 goto :error

echo [2/4] 发布 win-x86 自包含单文件...
dotnet publish "%PROJ%" -c Release -r win-x86 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=None -p:DebugSymbols=false -o "%ROOT%\publish\win-x86"
if errorlevel 1 goto :error

echo [3/4] 编译 x64 安装包...
"%INNO%" /DArch=x64 "%ROOT%\installer\OCC-Mission-Goals.iss"
if errorlevel 1 goto :error

echo [4/4] 编译 x86 安装包...
"%INNO%" /DArch=x86 "%ROOT%\installer\OCC-Mission-Goals.iss"
if errorlevel 1 goto :error

echo.
echo 完成。安装包位于:
echo   %ROOT%\output\
exit /b 0

:error
echo.
echo 构建失败，请检查上方输出。
exit /b 1
