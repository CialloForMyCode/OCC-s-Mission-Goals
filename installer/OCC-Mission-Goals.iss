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
#define MyAppVersion "0.2.0-rc"
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
; 始终显示目录选择页（全新安装可自定义位置；更新模式下由 Code 跳过）
DisableDirPage=no
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
VersionInfoVersion=0.2.0.0
VersionInfoProductVersion=0.2.0.0
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
; 主题配色文件：安装包只自带内置默认主题 Default.xaml，其余主题作为扩展中心可下载主题，不打包进安装程序
Source: "publish\win-{#Arch}\Themes\Default.xaml"; DestDir: "{app}\Themes"; Flags: ignoreversion
; 安装包只自带中文与英语，其余语言（日/韩/俄）作为扩展中心的可下载语言包，不打包进安装程序
Source: "publish\win-{#Arch}\Languages\zh.xaml"; DestDir: "{app}\Languages"; Flags: ignoreversion
Source: "publish\win-{#Arch}\Languages\en.xaml"; DestDir: "{app}\Languages"; Flags: ignoreversion
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  InstallModePage: TWizardPage;
  FreshRadio: TNewRadioButton;
  UpdateRadio: TNewRadioButton;
  ExistingDirLabel: TNewStaticText;
  SettingsPage: TWizardPage;
  LangLabel: TNewStaticText;
  LangCombo: TNewComboBox;

function IsChinese: Boolean;
begin
  Result := (ActiveLanguage() = 'chinesesimplified');
end;

{ 是否检测到已安装版本（Inno 通过 AppId 的卸载注册表项自动读取上次安装目录） }
function HasExistingInstall: Boolean;
begin
  Result := (WizardForm.PrevAppDir <> '');
end;

{ 按当前安装向导语言刷新「安装模式」页文案（保留已选状态）。 }
procedure LocalizeInstallModePage;
var
  canUpdate: Boolean;
begin
  canUpdate := HasExistingInstall;
  UpdateRadio.Enabled := canUpdate;

  if IsChinese then
  begin
    InstallModePage.Caption := '安装模式';
    InstallModePage.Description := '全新安装可选择安装位置；更新现有安装将覆盖到已安装目录，并保留原有配置与数据。';
    FreshRadio.Caption := '全新安装（可选择安装位置）';
    UpdateRadio.Caption := '更新现有安装';
    if canUpdate then
      ExistingDirLabel.Caption := '检测到已安装版本：' + WizardForm.PrevAppDir
    else
      ExistingDirLabel.Caption := '未检测到已安装版本，仅支持全新安装。';
  end
  else
  begin
    InstallModePage.Caption := 'Installation mode';
    InstallModePage.Description := 'A fresh install lets you choose the location. Updating installs over the existing copy and keeps your configuration and data.';
    FreshRadio.Caption := 'Fresh install (choose location)';
    UpdateRadio.Caption := 'Update existing installation';
    if canUpdate then
      ExistingDirLabel.Caption := 'Existing installation detected: ' + WizardForm.PrevAppDir
    else
      ExistingDirLabel.Caption := 'No existing installation detected. Only a fresh install is available.';
  end;
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
  { 安装模式选择页（欢迎页之后、目录页之前） }
  InstallModePage := CreateCustomPage(wpWelcome, '', '');

  FreshRadio := TNewRadioButton.Create(InstallModePage);
  FreshRadio.Parent := InstallModePage.Surface;
  FreshRadio.Top := 0;
  FreshRadio.Width := InstallModePage.SurfaceWidth;

  UpdateRadio := TNewRadioButton.Create(InstallModePage);
  UpdateRadio.Parent := InstallModePage.Surface;
  UpdateRadio.Top := FreshRadio.Top + FreshRadio.Height + ScaleY(6);
  UpdateRadio.Width := InstallModePage.SurfaceWidth;

  ExistingDirLabel := TNewStaticText.Create(InstallModePage);
  ExistingDirLabel.Parent := InstallModePage.Surface;
  ExistingDirLabel.Top := UpdateRadio.Top + UpdateRadio.Height + ScaleY(10);
  ExistingDirLabel.Width := InstallModePage.SurfaceWidth;
  ExistingDirLabel.Height := ScaleY(52);
  ExistingDirLabel.AutoSize := False;
  ExistingDirLabel.WordWrap := True;

  { 默认：有已安装版本则选「更新」，否则选「全新安装」 }
  if HasExistingInstall then
    UpdateRadio.Checked := True
  else
    FreshRadio.Checked := True;

  { 应用设置（语言）页，位于目录页之后 }
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
  LocalizeInstallModePage;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = InstallModePage.ID then
    LocalizeInstallModePage
  else if CurPageID = SettingsPage.ID then
    LocalizeSettingsPage;
end;

{ 更新模式下跳过目录选择页与语言设置页：直接覆盖到已安装目录并保留现有设置 }
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if not UpdateRadio.Checked then
    exit;

  if PageID = wpSelectDir then
  begin
    if WizardForm.PrevAppDir <> '' then
      WizardForm.DirEdit.Text := WizardForm.PrevAppDir;
    Result := True;
  end
  else if PageID = SettingsPage.ID then
    Result := True;
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
