; ShikkhaDesk — কোচিং সেন্টারের সম্পূর্ণ ব্যবস্থাপনা, এক জায়গায়।
; Built by ISCC from the published output in artifacts\app.
;
; The installer deliberately does three things a private-data app must do:
;   * installs per-machine under Program Files, with the database under ProgramData so that
;     uninstalling the program never deletes the centre's accounts;
;   * asks before removing user data, and never removes it silently;
;   * leaves no bundled adware, toolbar or third-party offer of any kind.

#define MyAppName "ShikkhaDesk"
#define MyAppNameBn "শিক্ষা ডেস্ক"
#define MyAppPublisher "ShikkhaDesk"
#define MyAppURL "https://github.com/kshohanservice-glitch/coaching-center-manager"
#define MyAppExeName "ShikkhaDesk.exe"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define DataDir "{commonappdata}\ShikkhaDesk"

[Setup]
AppId={{9F1B7A44-2C4E-4C7A-9E63-8B0E5A2D1C77}
AppName={#MyAppName} — {#MyAppNameBn}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=auto
LicenseFile=..\..\LICENSE
; Not signed: Windows Defender SmartScreen will warn. Signing needs a certificate the project
; does not have, and shipping a fake signature would be worse than shipping none.
SignedUninstaller=no
OutputDir=..\..\artifacts\release
OutputBaseFilename=ShikkhaDesk-Setup-v{#MyAppVersion}-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
WizardImageFile=.\wizard-image.bmp
WizardSmallImageFile=.\wizard-small-image.bmp
SetupIconFile=..\..\assets\branding\app-icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequiredOverridesAllowed=dialog
; Data lives outside {app}, so the data-removal question is asked explicitly below.
UninstallDisplayName={#MyAppName} {#MyAppVersion}
ChangesEnvironment=no
CloseApplications=force
RestartApplications=no
AllowNoIcons=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "ডেস্কটপে শর্টকাট রাখুন"; GroupDescription: "শর্টকাট:"; Flags: unchecked
Name: "startup"; Description: "কম্পিউটার চালু হলে স্বয়ংক্রিয়ভাবে চালু করুন"; GroupDescription: "সুবিধা:"; Flags: unchecked

[Files]
Source: "..\..\artifacts\app\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\artifacts\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\ডকুমেন্টেশন"; Filename: "{app}\docs"; Flags: skipifsourcedoesntexist
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startup

[Dirs]
; The database and backups are created here on first run, writable by standard users.
Name: "{#DataDir}"; Permissions: users-modify
Name: "{#DataDir}\Backups"; Permissions: users-modify
Name: "{#DataDir}\Logs"; Permissions: users-modify

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "শিক্ষা ডেস্ক চালু করুন"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
var
  DataRemovalPage: TInputOptionWizardPage;

procedure InitializeWizard();
begin
  DataRemovalPage := CreateInputOptionPage(wpSelectTasks,
    'তথ্য সম্পর্কে একটি প্রশ্ন',
    'আপনার কোচিং সেন্টারের হিসাব ও শিক্ষার্থীর তথ্য রাখবেন?',
    'এই তথ্যগুলো প্রোগ্রামের ফোল্ডারে নয়, একটি আলাদা নিরাপদ স্থানে থাকে। আপনি চাইলে এগুলো এখনই মুছে ফেলতে পারেন, অথবা রেখে দিতে পারেন — পরে যেকোনো সময় মুছতে পারবেন।',
    True, False);
  DataRemovalPage.Add('তথ্য রেখে দিন (সুপারিশকৃত) — পরে আবার ব্যবহার করতে পারবেন');
  DataRemovalPage.Add('সব তথ্য মুছে ফেলুন — হিসাব, শিক্ষার্থী, ব্যাকআপ সব মুছে যাবে');
  DataRemovalPage.SelectedValueIndex := 0;
end;

function ShouldRemoveData(): Boolean;
begin
  Result := (DataRemovalPage <> nil) and (DataRemovalPage.SelectedValueIndex = 1);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and ShouldRemoveData() then
  begin
    if DirExists(ExpandConstant('{#DataDir}')) then
      DelTree(ExpandConstant('{#DataDir}'), True, True, True);
  end;
end;
