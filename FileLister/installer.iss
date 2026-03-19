#define MyAppName "FileLister"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "FileLister"
#define MyAppExeName "FileLister.exe"
#define MyAppSourceDir "bin\Release\net8.0-windows\win-x64\publish"
#define MyOutputDir "installer_output"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppVerName={#MyAppName} {#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir={#MyOutputDir}
OutputBaseFilename=FileLister_Setup_v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; 관리자 권한 없이도 설치 가능 (사용자 폴더에 설치)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; 아이콘
SetupIconFile=AppIcon.ico
; 최소 OS: Windows 10
MinVersion=10.0

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 아이콘 만들기"; GroupDescription: "추가 작업:"; Flags: unchecked

[Files]
Source: "{#MyAppSourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; 시작 메뉴
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{#MyAppName} 제거"; Filename: "{uninstallexe}"
; 바탕화면 (선택)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "설치 후 {#MyAppName} 실행"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; 앱 설정 파일은 사용자 데이터이므로 제거하지 않음
Type: filesandordirs; Name: "{app}"
