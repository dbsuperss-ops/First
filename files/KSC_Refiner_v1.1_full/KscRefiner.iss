; KSC Refiner Inno Setup Script
; Inno Setup 6 이상 필요: https://jrsoftware.org/isinfo.php

#define AppName    "KSC Refiner"
#define AppVersion "1.2.0"
#define AppPublisher "Kyungshin Group"
#define AppURL     ""
#define AppExeName "KscRefiner.exe"
#define SrcRoot    "."

[Setup]
AppId={{A3F7C1B2-4D8E-4F9A-B0C3-12345678ABCD}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir={#SrcRoot}\installer_output
OutputBaseFilename=KscRefiner_Setup_v{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile=
; MinVersion=10.0

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; WPF UI 실행파일 (dotnet publish --self-contained 결과물)
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Python 엔진 (PyInstaller onedir 빌드 결과물)
Source: "ksc_refiner\dist\ksc_engine\*"; DestDir: "{app}\engine"; Flags: ignoreversion recursesubdirs createallsubdirs

; 환율 설정 파일 (기본값, 덮어쓰지 않음)
Source: "ksc_refiner\config\rates.json"; DestDir: "{app}\engine\config"; Flags: ignoreversion onlyifdoesntexist

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
