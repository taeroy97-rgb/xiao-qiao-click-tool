; 小乔点击工具 WPF 标准安装包
; 使用方式：ISCC.exe installer-wpf.iss

#define MyAppName "小乔点击工具"
#define MyAppVersion "1.1.7"
#define MyAppPublisher "小乔老师"
#define MyAppExeName "XiaoQiaoClickTool.exe"
#define SourceDir "publish\XiaoQiaoClickTool"

[Setup]
AppId={{B61D0D88-6C62-4FEA-96D2-9E5B4B8C8A21}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=..\安装包
OutputBaseFilename=小乔点击工具 1.1.7
SetupIconFile=XiaoQiaoClickTool\logo.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标："; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[InstallDelete]
Type: filesandordirs; Name: "{app}\*"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Run]
; 按需求：安装完成后默认不自动启动软件，避免遮挡安装完成页面。

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
