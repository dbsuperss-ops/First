"""
KSC Refiner Engine - PyInstaller entry point
ksc_engine.exe <input_dir> [year]
"""
import sys
import os

# PyInstaller 번들 실행 시 config 경로를 exe 위치 기준으로 설정
if getattr(sys, 'frozen', False):
    # 번들 exe 실행 중
    _BASE_DIR = os.path.dirname(sys.executable)
else:
    _BASE_DIR = os.path.dirname(os.path.abspath(__file__))

# 엔진이 config를 찾을 수 있도록 경로 주입
os.environ['KSC_CONFIG_DIR'] = os.path.join(_BASE_DIR, 'config')
os.environ['KSC_OUTPUT_DIR'] = os.path.join(_BASE_DIR, 'output')

# stdout UTF-8 강제 설정
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')

# 패키지 내부 모듈 경로 추가 (onedir 모드에서 필요)
if getattr(sys, 'frozen', False):
    sys.path.insert(0, os.path.join(sys._MEIPASS, 'ksc_refiner'))  # type: ignore
    sys.path.insert(0, sys._MEIPASS)                               # type: ignore

from engine import main  # noqa: E402

if __name__ == '__main__':
    input_dir = sys.argv[1] if len(sys.argv) > 1 else None
    year      = sys.argv[2] if len(sys.argv) > 2 else '2026'
    main(input_dir=input_dir, year=year)
