#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define PublishDir GetEnv("MACHINEOPS_PUBLISH_DIR")
#define ArtifactDir GetEnv("MACHINEOPS_ARTIFACT_DIR")

[Setup]
AppId={{464F5AB7-3024-4A34-8FF9-49BD72E8A9A8}
AppName=MachineOps Workbench
AppVersion={#MyAppVersion}
AppPublisher=Yottaverse
DefaultDirName={autopf}\MachineOps Workbench
DefaultGroupName=MachineOps Workbench
OutputDir={#ArtifactDir}
OutputBaseFilename=machineops-workbench-{#MyAppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\Yottaverse.MachineOps.Desktop.exe
WizardStyle=modern

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\MachineOps Workbench"; Filename: "{app}\Yottaverse.MachineOps.Desktop.exe"
Name: "{autodesktop}\MachineOps Workbench"; Filename: "{app}\Yottaverse.MachineOps.Desktop.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[Run]
Filename: "{app}\Yottaverse.MachineOps.Desktop.exe"; Description: "Launch MachineOps Workbench"; Flags: nowait postinstall skipifsilent
