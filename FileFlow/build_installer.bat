@echo off
chcp 65001 > nul
echo ============================================
echo  FileFlow — 인스톨러 빌드
echo ============================================
echo.

:: 경로 설정
set SCRIPT_DIR=%~dp0
set PUBLISH_DIR=%SCRIPT_DIR%publish
set ISCC=

:: Inno Setup 탐색
for %%p in (
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    "C:\Program Files\Inno Setup 6\ISCC.exe"
    "C:\Program Files (x86)\Inno Setup 5\ISCC.exe"
) do (
    if "!ISCC!"=="" if exist %%p set ISCC=%%p
)

:: EnableDelayedExpansion 없이 재시도
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" set ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe
if exist "C:\Program Files\Inno Setup 6\ISCC.exe"       set ISCC=C:\Program Files\Inno Setup 6\ISCC.exe
if exist "C:\Program Files (x86)\Inno Setup 5\ISCC.exe" set ISCC=C:\Program Files (x86)\Inno Setup 5\ISCC.exe

echo [1/2] dotnet publish ...
dotnet publish "%SCRIPT_DIR%FileFlow.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    /p:PublishSingleFile=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    -o "%PUBLISH_DIR%"

if errorlevel 1 (
    echo [오류] dotnet publish 실패.
    pause
    exit /b 1
)

echo.
echo [2/2] Inno Setup 인스톨러 빌드 ...

if "%ISCC%"=="" (
    echo [오류] Inno Setup을 찾을 수 없습니다.
    echo        https://jrsoftware.org/isinfo.php 에서 설치 후 다시 실행하시오.
    pause
    exit /b 1
)

"%ISCC%" "%SCRIPT_DIR%installer.iss"

if errorlevel 1 (
    echo [오류] 인스톨러 빌드 실패.
    pause
    exit /b 1
)

echo.
echo ============================================
echo  빌드 완료!
echo  결과물: installer_output\FileFlow_Setup_v1.0.0.exe
echo ============================================
pause
