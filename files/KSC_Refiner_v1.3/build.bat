@echo off
echo ============================================================
echo KSC Refiner v1.3 Build Script
echo ============================================================
echo.

REM 1. Python Engine Build (PyInstaller)
echo [1/4] Building Python engine...
echo.

REM Check PyInstaller installation
python -m pip show pyinstaller >nul 2>&1
if errorlevel 1 (
    echo PyInstaller not found. Installing...
    python -m pip install pyinstaller
)

REM Clean previous builds
if exist build rmdir /s /q build
if exist dist rmdir /s /q dist

REM Run PyInstaller
python -m PyInstaller ksc_engine.spec --noconfirm
if errorlevel 1 (
    echo.
    echo [ERROR] Python engine build failed
    pause
    exit /b 1
)

echo.
echo [OK] Python engine build complete
echo.

REM 2. C# Launcher Build
echo [2/4] Building C# launcher...
echo.

dotnet publish ksc_launcher\ksc_launcher.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained false ^
    -p:PublishSingleFile=true ^
    -p:PublishTrimmed=false ^
    -o publish
if errorlevel 1 (
    echo.
    echo [ERROR] C# launcher build failed
    pause
    exit /b 1
)

echo.
echo [OK] C# launcher build complete
echo.

REM 3. File Organization
echo [3/4] Organizing files...
echo.

REM Copy Python engine to publish folder
copy /y dist\ksc_engine.exe publish\
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to copy engine
    pause
    exit /b 1
)

REM Copy config folder
xcopy /y /e /i config publish\config
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to copy config files
    pause
    exit /b 1
)

REM Create output folder
if not exist publish\output mkdir publish\output

REM Copy README
copy /y README.md publish\
if errorlevel 1 (
    echo.
    echo [WARNING] README copy failed (continuing)
)

echo.
echo [OK] File organization complete
echo.

REM 4. Inno Setup Installer
echo [4/4] Creating installer...
echo.

REM Check Inno Setup path
set INNO_PATH="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not exist %INNO_PATH% (
    set INNO_PATH="C:\Program Files\Inno Setup 6\ISCC.exe"
)

if exist %INNO_PATH% (
    %INNO_PATH% KscRefiner.iss
    if errorlevel 1 (
        echo.
        echo [ERROR] Installer creation failed
        pause
        exit /b 1
    )
    echo.
    echo [OK] Installer creation complete
) else (
    echo.
    echo [WARNING] Inno Setup not found
    echo   Skipping installer creation
    echo   You can manually compile KscRefiner.iss with Inno Setup
)

echo.
echo ============================================================
echo Build Complete!
echo ============================================================
echo.
echo Output files:
echo   - Python engine: publish\ksc_engine.exe
echo   - C# launcher: publish\KscRefiner_v1.3.exe
if exist installer_output\KscRefiner_Setup_v1.3.0.exe (
    echo   - Installer: installer_output\KscRefiner_Setup_v1.3.0.exe
)
echo.

REM Open publish folder
explorer publish

pause
