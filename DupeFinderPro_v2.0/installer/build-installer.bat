@echo off
setlocal

echo ================================================
echo  DupeFinderPro v2.0 Installer Builder
echo ================================================
echo.

set ISCC=""
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" (
    set ISCC="%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
    goto :found
)
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" (
    set ISCC="%ProgramFiles%\Inno Setup 6\ISCC.exe"
    goto :found
)
if exist "C:\InnoSetup\ISCC.exe" (
    set ISCC="C:\InnoSetup\ISCC.exe"
    goto :found
)

echo [ERROR] Inno Setup 6 not found.
echo Download: https://jrsoftware.org/isdl.php
echo.
pause
exit /b 1

:found
echo [OK] Inno Setup found: %ISCC%
echo.

if not exist "..\publish\DupeFinderPro.exe" (
    echo [ERROR] publish\DupeFinderPro.exe not found.
    echo Run first: dotnet publish src\DupeFinderPro\DupeFinderPro.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
    echo.
    pause
    exit /b 1
)
echo [OK] Executable found.
echo.

if not exist "..\dist" mkdir "..\dist"

echo Building installer...
%ISCC% "DupeFinderPro.iss"

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Installer build failed. (code: %ERRORLEVEL%)
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ================================================
echo  Done! Installer: dist\DupeFinderPro_v2.0_Setup.exe
echo ================================================
echo.
pause
