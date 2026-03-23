@echo off
setlocal enabledelayedexpansion
chcp 65001 > nul
title KSC Refiner Build Script

echo ============================================================
echo  KSC Refiner v1.2 - 빌드 및 설치파일 생성
echo ============================================================
echo.

:: ── 경로 설정 ──────────────────────────────────────────────
set "SCRIPT_DIR=%~dp0"
set "REFINER_DIR=%SCRIPT_DIR%ksc_refiner"
set "UI_DIR=%SCRIPT_DIR%..\projects\SettlementAutomation_Hybrid\SettlementUI"
set "PUBLISH_DIR=%SCRIPT_DIR%publish"
set "INSTALLER_OUT=%SCRIPT_DIR%installer_output"

:: Python 위치 자동 탐지 (최신 버전 우선)
set "PYTHON="
for %%P in (
    "C:\Python314\python.exe"
    "C:\Python313\python.exe"
    "C:\Python312\python.exe"
    "C:\Python311\python.exe"
    "C:\Python310\python.exe"
    "C:\Python39\python.exe"
) do (
    if exist %%P (
        set "PYTHON=%%~P"
        goto :python_found
    )
)
:: PATH에서 찾기
where python >nul 2>&1
if %errorlevel%==0 (
    for /f "delims=" %%i in ('where python') do (
        set "PYTHON=%%i"
        goto :python_found
    )
)
echo [오류] Python을 찾을 수 없습니다. Python 3.10 이상을 설치해주세요.
pause & exit /b 1

:python_found
echo [OK] Python: %PYTHON%

:: dotnet 위치 확인
set "DOTNET=C:\Program Files\dotnet\dotnet.exe"
if not exist "%DOTNET%" (
    where dotnet >nul 2>&1
    if %errorlevel%==0 (
        set "DOTNET=dotnet"
    ) else (
        echo [오류] .NET SDK를 찾을 수 없습니다.
        pause & exit /b 1
    )
)
echo [OK] dotnet: %DOTNET%

:: Inno Setup 위치 확인
set "ISCC="
for %%I in (
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    "C:\Program Files\Inno Setup 6\ISCC.exe"
) do (
    if exist %%I set "ISCC=%%~I"
)
if defined ISCC (
    echo [OK] Inno Setup: %ISCC%
) else (
    echo [경고] Inno Setup 6가 없습니다. 설치파일(.exe) 생성을 건너뜁니다.
    echo        https://jrsoftware.org/isinfo.php 에서 설치 후 재실행하세요.
)

echo.
echo ─── STEP 1/3: Python 엔진 빌드 (PyInstaller) ───────────────
cd /d "%REFINER_DIR%"

:: PyInstaller 설치 확인
"%PYTHON%" -m pyinstaller --version >nul 2>&1
if errorlevel 1 (
    echo PyInstaller 설치 중...
    "%PYTHON%" -m pip install pyinstaller --quiet
    if errorlevel 1 (
        echo [오류] PyInstaller 설치 실패
        pause & exit /b 1
    )
)

:: openpyxl 설치 확인
"%PYTHON%" -c "import openpyxl" >nul 2>&1
if errorlevel 1 (
    echo openpyxl 설치 중...
    "%PYTHON%" -m pip install openpyxl --quiet
)

:: 이전 빌드 정리
if exist dist\ksc_engine rmdir /s /q dist\ksc_engine
if exist build\ksc_engine rmdir /s /q build\ksc_engine

echo PyInstaller 빌드 실행 중...
"%PYTHON%" -m PyInstaller ksc_engine.spec --noconfirm
if errorlevel 1 (
    echo [오류] PyInstaller 빌드 실패
    pause & exit /b 1
)

:: config 복사 (dist에 없는 경우)
if not exist "dist\ksc_engine\config" mkdir "dist\ksc_engine\config"
if not exist "dist\ksc_engine\config\rates.json" (
    copy /y "config\rates.json" "dist\ksc_engine\config\" >nul
)

echo [완료] Python 엔진 빌드 성공: %REFINER_DIR%\dist\ksc_engine\

echo.
echo ─── STEP 2/3: WPF UI 빌드 (dotnet publish) ──────────────────
if not exist "%UI_DIR%" (
    echo [경고] UI 프로젝트를 찾을 수 없습니다: %UI_DIR%
    echo        UI 빌드를 건너뜁니다.
    goto :installer_step
)

if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
mkdir "%PUBLISH_DIR%"

echo dotnet publish 실행 중...
"%DOTNET%" publish "%UI_DIR%\SettlementUI.csproj" ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    --output "%PUBLISH_DIR%" ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true
if errorlevel 1 (
    echo [오류] dotnet publish 실패
    pause & exit /b 1
)
echo [완료] WPF UI 빌드 성공: %PUBLISH_DIR%

:installer_step
echo.
echo ─── STEP 3/3: 설치파일 생성 (Inno Setup) ───────────────────
if not defined ISCC (
    echo [건너뜀] Inno Setup이 없어 설치파일 생성을 생략합니다.
    goto :done
)

if not exist "%INSTALLER_OUT%" mkdir "%INSTALLER_OUT%"

cd /d "%SCRIPT_DIR%"
echo Inno Setup 컴파일 중...
"%ISCC%" KscRefiner.iss
if errorlevel 1 (
    echo [오류] 설치파일 생성 실패
    pause & exit /b 1
)
echo [완료] 설치파일 생성 성공: %INSTALLER_OUT%\KscRefiner_Setup_v1.2.0.exe

:done
echo.
echo ============================================================
echo  빌드 완료!
echo.
echo  엔진  : %REFINER_DIR%\dist\ksc_engine\ksc_engine.exe
if exist "%PUBLISH_DIR%\KscRefiner.exe" (
    echo  UI    : %PUBLISH_DIR%\KscRefiner.exe
)
if defined ISCC (
    echo  설치파일: %INSTALLER_OUT%\KscRefiner_Setup_v1.2.0.exe
)
echo ============================================================
pause
