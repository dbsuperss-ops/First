#define MyAppName "WorkMonitor"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "WorkMonitor"
#define MyAppExeName "WorkMonitorWpf.exe"
#define PublishDir "publish"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=installer
OutputBaseFilename=WorkMonitor_Setup_{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
UsedUserAreasWarning=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 아이콘 만들기"; GroupDescription: "추가 아이콘:"; Flags: unchecked
Name: "startuprun"; Description: "Windows 시작 시 자동 실행"; GroupDescription: "시작 옵션:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\D3DCompiler_47_cor3.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\PenImc_cor3.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\PresentationNative_cor3.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\vcruntime140_cor3.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\wpfgfx_cor3.dll"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{#MyAppName} 제거"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; 자동 시작 등록은 현재 사용자 전용 (관리자 권한으로 실행 시에도 현재 사용자 HKCU에 등록)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startuprun; Check: IsAdminInstallMode

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "설치 완료 후 {#MyAppName} 실행"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-Command ""Stop-Process -Name WorkMonitorWpf -Force -ErrorAction SilentlyContinue"""; Flags: runhidden; RunOnceId: "StopApp"
