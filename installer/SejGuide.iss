; Inno Setup script – SEJ Location Guide (Windows installer)
; Built automatically by GitHub Actions:  ISCC /DMyAppVersion=1.0.N installer\SejGuide.iss

#define MyAppName "SEJ Location Guide"
#define MyAppExe "SejGuide.exe"
#define MyAuthor "DHARAM PAL JANGID, Senior Section Engineer (P.Way) Kota Division of WCRly"
#ifndef MyAppVersion
  #define MyAppVersion "1.0"
#endif

[Setup]
AppId={{8F3A2C1D-5B7E-4A9F-9C2D-1E6B7A8C9D01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAuthor}
AppCopyright=© {#MyAuthor}
AppComments=SEJ location and drawing selection as per IRPWM 2024 (ACS-16) and RDSO CT-48 (Nov 2025)
VersionInfoVersion={#MyAppVersion}
VersionInfoDescription={#MyAppName} Setup
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExe}
UninstallDisplayName={#MyAppName}
OutputDir=..\installer-output
OutputBaseFilename=SEJ-Location-Guide-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
MinVersion=10.0.17763
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "trustcert"; Description: "Trust the publisher certificate (Dharam Pal Jangid) on this PC – removes the 'Unknown publisher' warning for this app"; GroupDescription: "Security:"; Flags: unchecked

[Files]
Source: "..\publish\windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
Filename: "{sys}\certutil.exe"; Parameters: "-addstore -f Root ""{app}\SEJ-Location-Guide-Certificate.cer"""; Flags: runhidden waituntilterminated; Tasks: trustcert; StatusMsg: "Installing publisher certificate..."
Filename: "{sys}\certutil.exe"; Parameters: "-addstore -f TrustedPublisher ""{app}\SEJ-Location-Guide-Certificate.cer"""; Flags: runhidden waituntilterminated; Tasks: trustcert
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
