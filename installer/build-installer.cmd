@echo off
setlocal
REM ============================================================
REM  OCC's Mission & Goals - one-click Release installer build
REM
REM  Usage: double-click, or run installer\build-installer.cmd from repo root
REM  Output: [repo]\output\OCC-Mission-Goals-[version]-x64-setup.exe
REM                              OCC-Mission-Goals-[version]-x86-setup.exe
REM ============================================================

set "ROOT=%~dp0.."
REM Locate the Inno Setup compiler: classic per-machine path first, then the
REM 64-bit Program Files location, then a per-user install (/CURRENTUSER).
set "INNO=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not exist "%INNO%" set "INNO=C:\Program Files\Inno Setup 6\ISCC.exe"
if not exist "%INNO%" set "INNO=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not exist "%INNO%" set "INNO="
set "PROJ=%ROOT%\OCC's Mission & Goals.csproj"

REM The repo path contains a single quote (OCC's Mission & Goals), which
REM truncates the MSBuild item-transform inside dotnet publish and raises
REM MSB3094. Publish to a quote-free temp dir first, then copy back to the
REM repo's publish folder for Inno Setup to consume.
set "PUBTMP=%TEMP%\OCC-publish"

if "%INNO%"=="" (
    echo [ERROR] Inno Setup compiler not found ^(ISCC.exe^).
    echo         Install it with: winget install --id JRSoftware.InnoSetup --exact
    exit /b 1
)

echo [1/4] Publishing win-x64 self-contained single-file...
if exist "%PUBTMP%\win-x64" rmdir /s /q "%PUBTMP%\win-x64"
dotnet publish "%PROJ%" -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=None -p:DebugSymbols=false -o "%PUBTMP%\win-x64"
if errorlevel 1 goto :error
if exist "%ROOT%\publish\win-x64" rmdir /s /q "%ROOT%\publish\win-x64"
xcopy /E /I /Y "%PUBTMP%\win-x64" "%ROOT%\publish\win-x64" >nul
if errorlevel 1 goto :error

echo [2/4] Publishing win-x86 self-contained single-file...
if exist "%PUBTMP%\win-x86" rmdir /s /q "%PUBTMP%\win-x86"
dotnet publish "%PROJ%" -c Release -r win-x86 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=None -p:DebugSymbols=false -o "%PUBTMP%\win-x86"
if errorlevel 1 goto :error
if exist "%ROOT%\publish\win-x86" rmdir /s /q "%ROOT%\publish\win-x86"
xcopy /E /I /Y "%PUBTMP%\win-x86" "%ROOT%\publish\win-x86" >nul
if errorlevel 1 goto :error

echo [3/4] Compiling x64 installer...
"%INNO%" /DArch=x64 "%ROOT%\installer\OCC-Mission-Goals.iss"
if errorlevel 1 goto :error

echo [4/4] Compiling x86 installer...
"%INNO%" /DArch=x86 "%ROOT%\installer\OCC-Mission-Goals.iss"
if errorlevel 1 goto :error

echo.
echo Done. Installers are in the repo's output folder.
exit /b 0

:error
echo.
echo Build failed. See output above.
exit /b 1
