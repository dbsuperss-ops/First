@echo off
setlocal

echo ================================================
echo  DupeFinderPro v2.0 Installer Builder
echo ================================================
echo.

:: Inno Setup 설치 경로 탐색
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

echo [오류] Inno Setup 6을 찾을 수 없습니다.
echo 다음 주소에서 다운로드하세요: https://jrsoftware.org/isdl.php
echo.
pause
exit /b 1

:found
echo [OK] Inno Setup 발견: %ISCC%
echo.

:: 빌드된 실행 파일 확인
if not exist "..\publish\DupeFinderPro.exe" (
    echo [오류] publish\DupeFinderPro.exe 가 없습니다.
    echo 먼저 dotnet publish 를 실행하세요:
    echo   dotnet publish src\DupeFinderPro\DupeFinderPro.csproj -c Release -r win-x64 --self-contained true -o publish
    echo.
    pause
    exit /b 1
)
echo [OK] 실행 파일 확인 완료
echo.

:: dist 폴더 생성
if not exist "..\dist" mkdir "..\dist"

:: 인스톨러 빌드
echo 인스톨러 빌드 중...
%ISCC% "DupeFinderPro.iss"

if %ERRORLEVEL% neq 0 (
    echo.
    echo [오류] 인스톨러 빌드 실패 (오류 코드: %ERRORLEVEL%)
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ================================================
echo  완료! 인스톨러 위치: dist\DupeFinderPro_v2.0_Setup.exe
echo ================================================
echo.
pause
