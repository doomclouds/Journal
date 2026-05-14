#define AppName "Journal"
#ifndef AppVersion
#define AppVersion "0.1.0"
#endif
#define Publisher "panyonglin"
#define PublishRoot "..\..\artifacts\installer\publish\Journal"
#define OutputRoot "..\..\artifacts\installer\dist"

[Setup]
AppId={{B82A4615-4424-4C2C-9C66-35C1E8968F10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
AppPublisherURL=https://github.com/panyonglin
AppSupportURL=https://github.com/panyonglin
DefaultDirName={autopf}\Journal
DefaultGroupName=Journal
DisableProgramGroupPage=yes
OutputDir={#OutputRoot}
OutputBaseFilename=Journal-Setup-{#AppVersion}
SetupIconFile=..\..\assets\app-icon\journal.ico
WizardImageFile=assets\wizard-large.bmp
WizardSmallImageFile=assets\wizard-small.bmp
UninstallDisplayIcon={app}\assets\journal.ico
LicenseFile=..\..\LICENSE
InfoBeforeFile=assets\info-before.rtf
InfoAfterFile=assets\info-after.rtf
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "{#PublishRoot}\app\*"; DestDir: "{app}\app"; Flags: recursesubdirs ignoreversion
Source: "{#PublishRoot}\backend\*"; DestDir: "{app}\backend"; Flags: recursesubdirs ignoreversion
Source: "{#PublishRoot}\legal\*"; DestDir: "{app}\legal"; Flags: recursesubdirs ignoreversion
Source: "{#PublishRoot}\assets\journal.ico"; DestDir: "{app}\assets"; Flags: ignoreversion

[Icons]
Name: "{group}\Journal"; Filename: "{app}\app\Journal.exe"; IconFilename: "{app}\assets\journal.ico"
Name: "{autodesktop}\Journal"; Filename: "{app}\app\Journal.exe"; IconFilename: "{app}\assets\journal.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\app\Journal.exe"; Description: "启动 Journal"; Flags: nowait postinstall skipifsilent unchecked runasoriginaluser
