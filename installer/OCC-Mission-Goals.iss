; ============================================================
;  OCC's Mission & Goals — Inno Setup 安装脚本
;
;  用法（在仓库根目录下，用 ISCC.exe 编译）：
;    x64:  ISCC.exe /DArch=x64 installer\OCC-Mission-Goals.iss
;    x86:  ISCC.exe /DArch=x86 installer\OCC-Mission-Goals.iss
;
;  前置条件：先执行 dotnet publish 生成 publish\win-x64 与
;            publish\win-x86 下的自包含单文件 OCCMissionGoals.exe。
; ============================================================

#ifndef Arch
  #define Arch "x64"
#endif

#define MyAppName "OCC's Mission & Goals"
#define MyAppVersion "0.1.8-Beta"
#define MyAppPublisher "Harvnyx"
#define MyAppURL "https://github.com/CialloForMyCode/OCC-s-Mission-Goals"
#define MyAppExeName "OCCMissionGoals.exe"

[Setup]
; Source 路径相对于仓库根目录解析（脚本位于 installer/ 下）
SourceDir=..

; 固定的 AppId，后续版本升级请勿修改
AppId={{90585DF3-1470-45B3-9866-D6BE93E4C27B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright=Harvnyx © 2022-2026
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; 应用会把 config.ini 与 Projects/ 写在 exe 同目录下，
; 因此按用户安装到可写的 %LOCALAPPDATA%\Programs，无需管理员权限。
DefaultDirName={userpf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

OutputDir=output
OutputBaseFilename=OCC-Mission-Goals-{#MyAppVersion}-{#Arch}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; 强制显示「选择安装语言」对话框，避免在中文系统上自动匹配后跳过语言选择
LanguageDetectionMethod=none

#if Arch == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#else
ArchitecturesAllowed=x86compatible
#endif

; setup.exe 自身的版本信息（Windows 要求纯数字版本段）
VersionInfoVersion=0.1.8.0
VersionInfoProductVersion=0.1.8.0
VersionInfoTextVersion={#MyAppVersion}
VersionInfoProductTextVersion={#MyAppVersion}

UninstallDisplayName={#MyAppName}

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Languages\English.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\win-{#Arch}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\win-{#Arch}\Languages\*.xaml"; DestDir: "{app}\Languages"; Flags: ignoreversion
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  SettingsPage: TWizardPage;
  LangLabel: TNewStaticText;
  LangCombo: TNewComboBox;

function IsChinese: Boolean;
begin
  Result := (ActiveLanguage() = 'chinesesimplified');
end;

{ 按当前安装向导语言刷新页面标题、标签与下拉框选项（保留已选索引）。 }
procedure LocalizeSettingsPage;
var
  selLang: Integer;
begin
  selLang := LangCombo.ItemIndex;
  if selLang < 0 then selLang := 0;

  if IsChinese then
  begin
    SettingsPage.Caption := '应用设置';
    SettingsPage.Description := '配置应用的语言。该设置会写入 config.ini，之后也可在应用内的「设置」页面修改。';
    LangLabel.Caption := '界面语言';
  end
  else
  begin
    SettingsPage.Caption := 'Application settings';
    SettingsPage.Description := 'Configure the application language. This is written to config.ini and can be changed later in the app''s Settings page.';
    LangLabel.Caption := 'Language';
  end;

  { 语言名称始终以自身语言显示，不随向导语言变化 }
  LangCombo.Items.Clear;
  LangCombo.Items.Add('中文');
  LangCombo.Items.Add('English');

  LangCombo.ItemIndex := selLang;
end;

procedure InitializeWizard;
begin
  SettingsPage := CreateCustomPage(wpSelectDir, '', '');

  LangLabel := TNewStaticText.Create(SettingsPage);
  LangLabel.Parent := SettingsPage.Surface;
  LangLabel.Top := 0;

  LangCombo := TNewComboBox.Create(SettingsPage);
  LangCombo.Parent := SettingsPage.Surface;
  LangCombo.Style := csDropDownList;
  LangCombo.Width := SettingsPage.SurfaceWidth;
  LangCombo.Top := ScaleY(22);

  { 填充选项并设置默认值（语言=中文） }
  LocalizeSettingsPage;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = SettingsPage.ID then
    LocalizeSettingsPage;
end;

{ 安装完成后写入 config.ini（仅在文件不存在时创建，避免升级时覆盖用户已有设置） }
procedure CurStepChanged(CurStep: TSetupStep);
var
  CfgPath: string;
  LangVal, Content: string;
begin
  if CurStep = ssPostInstall then
  begin
    CfgPath := ExpandConstant('{app}\config.ini');
    if not FileExists(CfgPath) then
    begin
      if LangCombo.ItemIndex = 1 then
        LangVal := 'en'
      else
        LangVal := 'zh';

      Content := '[General]' + #13#10 +
                 'language = ' + LangVal + #13#10;

      SaveStringToFile(CfgPath, Content, False);
    end;
  end;
end;
