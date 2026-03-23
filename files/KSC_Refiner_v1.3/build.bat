@echo off
chcp 65001 > nul
echo ============================================================
echo KSC Refiner v1.3 빌드 스크립트
echo ============================================================
echo.

REM 1. Python 엔진 빌드 (PyInstaller)
echo [1/4] Python 엔진 빌드 중...
echo.

REM PyInstaller 설치 확인
python -m pip show pyinstaller >nul 2>&1
if errorlevel 1 (
    echo PyInstaller가 설치되어 있지 않습니다. 설치 중...
    python -m pip install pyinstaller
)

REM 기존 빌드 삭제
if exist build rmdir /s /q build
if exist dist rmdir /s /q dist

REM PyInstaller 실행
python -m PyInstaller ksc_engine.spec --noconfirm
if errorlevel 1 (
    echo.
    echo ❌ Python 엔진 빌드 실패
    pause
    exit /b 1
)

echo.
echo ✅ Python 엔진 빌드 완료
echo.

REM 2. C# 런처 빌드
echo [2/4] C# 런처 빌드 중...
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
    echo ❌ C# 런처 빌드 실패
    pause
    exit /b 1
)

echo.
echo ✅ C# 런처 빌드 완료
echo.

REM 3. 파일 정리
echo [3/4] 파일 정리 중...
echo.

REM publish 폴더에 Python 엔진 복사
copy /y dist\ksc_engine.exe publish\
if errorlevel 1 (
    echo.
    echo ❌ 파일 복사 실패
    pause
    exit /b 1
)

REM config 폴더 복사
xcopy /y /e /i config publish\config
if errorlevel 1 (
    echo.
    echo ❌ 설정 파일 복사 실패
    pause
    exit /b 1
)

REM output 폴더 생성
if not exist publish\output mkdir publish\output

REM README 복사
copy /y README.md publish\
if errorlevel 1 (
    echo.
    echo ⚠ README 복사 실패 (계속 진행)
)

echo.
echo ✅ 파일 정리 완료
echo.

REM 4. Inno Setup으로 설치 파일 생성
echo [4/4] 설치 파일 생성 중...
echo.

REM Inno Setup 경로 확인
set INNO_PATH="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not exist %INNO_PATH% (
    set INNO_PATH="C:\Program Files\Inno Setup 6\ISCC.exe"
)

if exist %INNO_PATH% (
    %INNO_PATH% KscRefiner.iss
    if errorlevel 1 (
        echo.
        echo ❌ 설치 파일 생성 실패
        pause
        exit /b 1
    )
    echo.
    echo ✅ 설치 파일 생성 완료
) else (
    echo.
    echo ⚠ Inno Setup이 설치되어 있지 않습니다.
    echo   설치 파일 생성을 건너뜁니다.
    echo   수동으로 KscRefiner.iss를 Inno Setup으로 컴파일하세요.
)

echo.
echo ============================================================
echo 빌드 완료!
echo ============================================================
echo.
echo 출력 파일:
echo   - Python 엔진: publish\ksc_engine.exe
echo   - C# 런처: publish\KscRefiner_v1.3.exe
if exist installer_output\KscRefiner_Setup_v1.3.0.exe (
    echo   - 설치 프로그램: installer_output\KscRefiner_Setup_v1.3.0.exe
)
echo.

REM publish 폴더 열기
explorer publish

pause
