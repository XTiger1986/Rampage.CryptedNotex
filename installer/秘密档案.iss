#define MyAppName "秘密档案"
#define MyAppVersion "1.0.0"
#define MyAppExe "Rampage.CryptedNotex.WinForm.exe"
#define PublishDir "..\Rampage.CryptedNotex.Net\Rampage.CryptedNotex.WinForm\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{edfe1ee5-0900-4da7-bbe4-8e359e495f66}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=XTiger1986
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableDirPage=no
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
UsePreviousAppDir=yes
OutputDir=output
OutputBaseFilename=秘密档案-Setup-{#MyAppVersion}
SetupIconFile=..\Rampage.CryptedNotex.Net\Rampage.CryptedNotex.WinForm\Assets\Chrome\app-icon.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter={#MyAppExe}
UninstallDisplayIcon={app}\{#MyAppExe}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "data\*,__Logs\*,themes\*"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "启动{#MyAppName}"; Flags: nowait postinstall skipifsilent