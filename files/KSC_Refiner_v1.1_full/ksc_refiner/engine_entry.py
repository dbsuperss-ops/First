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

# stdout UTF-8 강제 설정 (Windows cp949 에러 방지)
if hasattr(sys.stdout, 'reconfigure'):
    try:
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
        sys.stderr.reconfigure(encoding='utf-8', errors='replace')
    except Exception:
        # reconfigure 실패 시 wrapper로 감싸기
        import io
        sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace', line_buffering=True)
        sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace', line_buffering=True)
else:
    # Python 3.6 이하 또는 stdout이 buffer가 없는 경우
    import io
    if hasattr(sys.stdout, 'buffer'):
        sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace', line_buffering=True)
        sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace', line_buffering=True)

from engine import main  # noqa: E402

if __name__ == '__main__':
    input_dir = sys.argv[1] if len(sys.argv) > 1 else None
    year      = sys.argv[2] if len(sys.argv) > 2 else '2026'
    main(input_dir=input_dir, year=year)
