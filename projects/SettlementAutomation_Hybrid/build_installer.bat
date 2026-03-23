@echo off
setlocal EnableDelayedExpansion

set "ROOT=%~dp0"
set "KSCR=%ROOT%..\..\KSC_Refiner_v1.1_full\ksc_refiner"
set "UI_PROJ=%ROOT%SettlementUI\SettlementUI.csproj"
set "STAGE=%ROOT%installer\staging"
set "PYINSTALLER=C:\python311\Scripts\pyinstaller.exe"
set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

echo ============================================================
echo  KSC Refiner 설치파일 빌드
echo ============================================================

:: ── 1. 스테이징 폴더 초기화 ──────────────────────────────────
echo.
echo [1/4] 스테이징 폴더 초기화...
if exist "%STAGE%" rmdir /s /q "%STAGE%"
mkdir "%STAGE%\ksc_refiner"

:: ── 2. WPF 퍼블리시 (self-contained, single-file) ─────────────
echo.
echo [2/4] WPF 퍼블리시 중...
dotnet publish "%UI_PROJ%" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    /p:PublishSingleFile=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    -o "%STAGE%"
if %ERRORLEVEL% neq 0 (
    echo [오류] dotnet publish 실패
    exit /b 1
)

:: ── 3. PyInstaller로 engine.exe 빌드 ─────────────────────────
echo.
echo [3/4] PyInstaller engine.exe 빌드 중...
pushd "%KSCR%"
"%PYINSTALLER%" ^
    --onedir ^
    --name engine ^
    --distpath "%STAGE%\ksc_refiner" ^
    --workpath "%ROOT%installer\pyinstaller_work" ^
    --specpath "%ROOT%installer" ^
    --noconfirm ^
    engine.py
if %ERRORLEVEL% neq 0 (
    echo [오류] PyInstaller 빌드 실패
    popd
    exit /b 1
)
popd

:: engine 서브폴더 안 내용을 ksc_refiner 바로 아래로 올리기
::   staging/ksc_refiner/engine/ → staging/ksc_refiner/
if exist "%STAGE%\ksc_refiner\engine" (
    xcopy /E /Y /Q "%STAGE%\ksc_refiner\engine\*" "%STAGE%\ksc_refiner\"
    rmdir /s /q "%STAGE%\ksc_refiner\engine"
)

:: config 폴더 복사 (engine.exe 옆에 위치해야 함)
xcopy /E /Y /Q "%KSCR%\config\*" "%STAGE%\ksc_refiner\config\"

:: ── 4. Inno Setup 빌드 ───────────────────────────────────────
echo.
echo [4/4] Inno Setup 설치 파일 생성 중...
"%ISCC%" "%ROOT%installer\setup.iss"
if %ERRORLEVEL% neq 0 (
    echo [오류] Inno Setup 빌드 실패
    exit /b 1
)

echo.
echo ============================================================
echo  완료: installer\output\KSCRefinerSetup.exe
echo ============================================================
endlocal
