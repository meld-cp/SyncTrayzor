#define AppExeName "SyncTrayzor.exe"
#define AppRoot ".."
#define AppBin AppRoot + "\dist"
#define AppSrc AppRoot + "\src\SyncTrayzor"
#define AppExe AppBin + "\SyncTrayzor.exe"
#define AppName GetStringFileInfo(AppExe, "ProductName")
#define AppVersion GetVersionNumbersString(AppExe)
#define AppPublisher "SyncTrayzor"
#define AppURL "https://github.com/GermanCoding/SyncTrayzor"
#define AppDataFolder "SyncTrayzor"

[Setup]
AppId={{#AppId}
AppName={#AppName} ({#Arch})
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
;AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir="."
OutputBaseFilename={#AppName}Setup-{#Arch}
SetupIconFile={#AppSrc}\Icons\default.ico
WizardSmallImageFile=icon.bmp
Compression=lzma2/max
;Compression=None
SolidCompression=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes
RestartApplications=no
; If we try and close CefSharp.BrowserSubprocess.exe we'll fail - it doesn't respond well
; However if we close *just* SyncTrayzor, that will take care of shutting down CefSharp and syncthing
CloseApplicationsFilter=SyncTrayzor.exe
TouchDate=current
WizardStyle=modern
; We do access user areas, but only as a best-effort attempt to clean up after ourselves
UsedUserAreasWarning=no
#if "x64" == Arch
ArchitecturesInstallIn64BitMode=x64os
ArchitecturesAllowed=x64os
#else
ArchitecturesInstallIn64BitMode=arm64
ArchitecturesAllowed=arm64
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Dirs]
Name: "{userappdata}\{#AppDataFolder}"

[Files]
Source: "{#AppBin}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall; Parameters: {code:SyncTrayzorStartFlags}; Check: ShouldStartSyncTrayzor

[Code]
procedure BumpInstallCount;
var
  FileContents: AnsiString;
  InstallCount: integer;
begin
  { Increment the install count in InstallCount.txt if it exists, or create it with the contents '1' if it doesn't }
  if LoadStringFromFile(ExpandConstant('{app}\InstallCount.txt'), FileContents) then
  begin
    InstallCount := StrTointDef(Trim(string(FileContents)), 0) + 1;
  end
  else
  begin
    InstallCount := 1;
  end;

  SaveStringToFile(ExpandConstant('{app}\InstallCount.txt'), IntToStr(InstallCount), False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  FindRec: TFindRec;
  FolderPath: String;
  FilePath: String;
  ExeConfig: String;
begin
  if CurStep = ssInstall then
  begin
    BumpInstallCount();

    { We might be being run from ProcessRunner.exe, *and* we might be trying to update it. Funsies. Let's rename it (which Windows lets us do) }
    DeleteFile(ExpandConstant('{app}\ProcessRunner.exe.old'));
    RenameFile(ExpandConstant('{app}\ProcessRunner.exe'), ExpandConstant('{app}\ProcessRunner.exe.old'));

    Log(ExpandConstant('Looking for resource files in {app}\*'));
    { Remove resource files. This means that out-of-date languages will be removed, which (as a last-ditch resore) will alert maintainers that something's wrong }
    if FindFirst(ExpandConstant('{app}\*'), FindRec) then
    begin
      try
        repeat
          if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0) and (FindRec.Name <> '.') and (FindRec.Name <> '..') then
          begin
            FolderPath :=  ExpandConstant('{app}\') + FindRec.Name;
            FilePath := FolderPath + '\SyncTrayzor.resources.dll';
            if DeleteFile(FilePath) then
            begin
              Log('Deleted ' + FilePath);
              if DelTree(FolderPath, True, False, False) then
                Log('Deleted ' + FolderPath);
            end;
          end;
        until not FindNext(FindRec);
      finally
        FindClose(FindRec);
      end;
    end;
  end
  else if CurStep = ssPostInstall then
  begin
    ExeConfig := ExpandConstant('{param:SyncTrayzorExeConfig}');
    if ExeConfig <> '' then
    begin
      if FileExists(ExeConfig) then
      begin
        CopyFile(ExeConfig, ExpandConstant('{app}\SyncTrayzor.exe.config'), false);
      end
      else
      begin
        MsgBox('Could not find SyncTrayzorExeConfig file: ' + ExeConfig + '. Using default.', mbError, MB_OK);
      end
    end
  end
end;

function CmdLineParamGiven(const Value: String): Boolean;
var
  I: Integer;  
begin
  // Can't use {param}, as it doesn't match flags with no value
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), Value) = 0 then
    begin
      Result := True;
      Exit;
    end;
end;

function ShouldStartSyncTrayzor(): Boolean;
begin
  Result := (not WizardSilent()) or CmdLineParamGiven('/StartSyncTrayzor');
end;

function SyncTrayzorStartFlags(param: String): String;
begin
   if WizardSilent() then begin
      Result := '-minimized'
   end else begin
      Result := ''
   end;
end;

// We won't be able to find keys for users other than the one running the installer, but try and do
// a best-effort attempt to cleaning ourselves up.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  keyValueNames: TArrayOfString;
  keyValue: String;
  i: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if RegGetValueNames(HKEY_CURRENT_USER, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run', keyValueNames) then
    begin
      for i := 0 to GetArrayLength(keyValueNames)-1 do
      begin
        if Pos('SyncTrayzor', keyValueNames[i]) = 1 then
        begin
          if RegQueryStringValue(HKEY_CURRENT_USER, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run', keyValueNames[i], keyValue) then
          begin
            if Pos(ExpandConstant('"{app}\{#AppExeName}"'), keyValue) = 1 then
            begin
              RegDeleteValue(HKEY_CURRENT_USER, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run', keyValueNames[i]);
            end;
          end;
        end
      end;
    end;
  end;
end;

[UninstallRun]
Filename: "{app}\SyncTrayzor.exe"; Parameters: "--shutdown"; RunOnceId: "ShutdownSyncTrayzor"; Flags: skipifdoesntexist

[UninstallDelete]
Type: files; Name: "{app}\ProcessRunner.exe.old"
Type: files; Name: "{app}\InstallCount.txt"
; Not sure why this gets left behind, but it does. Clean it up...
Type: filesandordirs; Name: "{app}\locales"
Type: filesandordirs; Name: "{userappdata}\{#AppDataFolder}"
Type: filesandordirs; Name: "{localappdata}\{#AppDataFolder}"

