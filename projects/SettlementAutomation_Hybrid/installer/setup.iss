#define MyAppName      "KSC Refiner"
#define MyAppVersion   "1.1"
#define MyAppPublisher "KSC"
#define MyAppExeName   "SettlementUI.exe"
#define MyStaging      "..\installer\staging"
#define MyAppIcon      "chart.ico"

[Setup]
AppId={{A3F2B8C1-7D4E-4F9A-B2C3-1E5D6F7A8B9C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=output
OutputBaseFilename=KSCRefinerSetup
SetupIconFile={#MyAppIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; WPF 실행파일 (self-contained single-file)
Source: "{#MyStaging}\SettlementUI.exe"; DestDir: "{app}"; Flags: ignoreversion

; ksc_refiner 폴더 전체 (engine.exe + Python 런타임 DLL + config/)
Source: "{#MyStaging}\ksc_refiner\*"; DestDir: "{app}\ksc_refiner"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
