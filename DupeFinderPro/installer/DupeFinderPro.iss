#define MyAppName "DupeFinderPro"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "DupeFinderPro"
#define MyAppExeName "DupeFinderPro.exe"
#define MyAppId "{{A3F2C1D4-8B7E-4F9A-B2C5-1D6E3A9F0B4C}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=..\installer_output
OutputBaseFilename=DupeFinderPro_Setup_v{#MyAppVersion}
SetupIconFile=..\src\DupeFinderPro\Assets\duple.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 바로가기 만들기"; GroupDescription: "추가 작업:"; Flags: unchecked

[Files]
Source: "..\src\DupeFinderPro\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{#MyAppName} 제거"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "설치 후 {#MyAppName} 실행"; Flags: nowait postinstall skipifsilent
