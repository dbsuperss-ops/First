# -*- mode: python ; coding: utf-8 -*-
# PyInstaller spec for KSC Refiner Engine
# Usage: pyinstaller ksc_engine.spec

import os

block_cipher = None

a = Analysis(
    ['engine_entry.py'],
    pathex=['.'],
    binaries=[],
    datas=[
        ('config/rates.json', 'config'),    # 환율 설정 번들
        ('config/settings.json', 'config'), # 설정 파일 번들
    ],
    hiddenimports=[
        'openpyxl',
        'openpyxl.styles',
        'openpyxl.utils',
        'openpyxl.workbook',
        'openpyxl.worksheet',
        'openpyxl.reader',
        'openpyxl.writer',
        'parsers',
        'parsers.ksccz_parser',
        'parsers.kscp_parser',
        'parsers.kctr_parser',
        'parsers.ksce_parser',
        'parsers.ksci_parser',
        'schema',
        'engine',
    ],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=['tkinter', 'matplotlib', 'numpy', 'pandas', 'PIL'],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name='ksc_engine',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=True,       # 콘솔 출력 활성 (UI에서 stdout 읽음)
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon=None,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.zipfiles,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name='ksc_engine',
)
