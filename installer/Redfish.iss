; Build the Release configuration before compiling this script with Inno Setup 6.4 or later.
; ISCC.exe installer\Redfish.iss
; Optional overrides: /DBuildDir="C:\path\to\Release" /DAppVersion=1.2.3
#ifndef BuildDir
  #define BuildDir SourcePath + "..\Redfish\bin\Release"
#endif
#if !FileExists(BuildDir + "\Redfish.exe")
  #error Build Redfish.sln in Release mode before compiling the installer.
#endif
#ifndef AppVersion
  #define AppVersion GetVersionNumbersString(BuildDir + "\Redfish.exe")
#endif

[Setup]
AppId={{E53F39C5-5572-451B-A302-382A592EB6F4}
AppName=Redfish
AppVersion={#AppVersion}
AppPublisher=Skyjos
AppPublisherURL=https://www.skyjos.com
AppSupportURL=https://github.com/skyjos-com/Redfish
DefaultDirName={autopf}\Redfish
DefaultGroupName=Redfish
DisableProgramGroupPage=yes
PrivilegesRequired=admin
MinVersion=6.1sp1
; Include ARM64 Windows capable of running x86/x64 applications through emulation.
ArchitecturesAllowed=x86compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=RedfishSetup-{#AppVersion}
UninstallDisplayIcon={app}\Redfish.exe
UninstallDisplayName=Redfish
AppMutex=Global\Skyjos.Redfish.UI
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
UninstallLogging=yes
WizardStyle=modern
Compression=lzma2
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "service"; Description: "Register the Windows service (configure and start it from Redfish)"; GroupDescription: "Service:"; Flags: unchecked

[Files]
; List runtime files explicitly: do not ship old service executables, logs, settings, or debug symbols.
Source: "{#BuildDir}\Redfish.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\Redfish.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\zh-Hans\Redfish.resources.dll"; DestDir: "{app}\zh-Hans"; Flags: ignoreversion
Source: "{#BuildDir}\ja\Redfish.resources.dll"; DestDir: "{app}\ja"; Flags: ignoreversion
Source: "{#BuildDir}\de\Redfish.resources.dll"; DestDir: "{app}\de"; Flags: ignoreversion
Source: "{#BuildDir}\SMBLibrary.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\SMBLibrary.Win32.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Redfish"; Filename: "{app}\Redfish.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Redfish"; Filename: "{app}\Redfish.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Redfish.exe"; Description: "Launch Redfish"; Flags: nowait postinstall skipifsilent runasoriginaluser; Check: CanLaunchRedfish

; ProgramData settings are owned by Redfish, not setup. Never create, migrate, or remove them here.
; There is intentionally no [UninstallDelete] entry for settings or application-generated files.

[Code]
const
  ServiceName = 'RedfishService';
  ServiceRegistryKey = 'SYSTEM\CurrentControlSet\Services\RedfishService';
  ServiceStopped = 1;
  ServiceStopPending = 3;
  ServiceRunning = 4;

type
  TServiceStatus = record
    ServiceType: Cardinal;
    CurrentState: Cardinal;
    ControlsAccepted: Cardinal;
    Win32ExitCode: Cardinal;
    ServiceSpecificExitCode: Cardinal;
    CheckPoint: Cardinal;
    WaitHint: Cardinal;
  end;

var
  HadService: Boolean;
  RestartService: Boolean;
  ServiceSetupFailed: Boolean;

function OpenSCManager(MachineName, DatabaseName: Integer; Access: Cardinal): THandle;
  external 'OpenSCManagerW@advapi32.dll stdcall';
function OpenService(Manager: THandle; Name: String; Access: Cardinal): THandle;
  external 'OpenServiceW@advapi32.dll stdcall';
function QueryServiceStatus(Service: THandle; var Status: TServiceStatus): Boolean;
  external 'QueryServiceStatus@advapi32.dll stdcall';
function ControlService(Service: THandle; Control: Cardinal; var Status: TServiceStatus): Boolean;
  external 'ControlService@advapi32.dll stdcall';
function StartService(Service: THandle; ArgCount: Cardinal; Args: Integer): Boolean;
  external 'StartServiceW@advapi32.dll stdcall';
function CloseServiceHandle(Handle: THandle): Boolean;
  external 'CloseServiceHandle@advapi32.dll stdcall';
function NativeLastError: Cardinal;
  external 'GetLastError@kernel32.dll stdcall';

function ServiceExists: Boolean;
begin
  Result := RegKeyExists(HKLM, ServiceRegistryKey);
end;

function OwnsService: Boolean;
var
  ImagePath: String;
begin
  Result := RegQueryStringValue(HKLM, ServiceRegistryKey, 'ImagePath', ImagePath) and
    (CompareText(Trim(ImagePath), '"' + ExpandConstant('{app}\Redfish.exe') + '" --service') = 0);
end;

function ChangeServiceState(Running: Boolean; var WasRunning: Boolean): String;
var
  Manager, Service: THandle;
  Status: TServiceStatus;
  Attempt: Integer;
  TargetState: Cardinal;
  RequestSent: Boolean;
begin
  Result := '';
  WasRunning := False;
  Manager := OpenSCManager(0, 0, 1);
  if Manager = 0 then begin
    Result := SysErrorMessage(NativeLastError);
    Exit;
  end;
  try
    Service := OpenService(Manager, ServiceName, $0004 or $0010 or $0020);
    if Service = 0 then begin
      Result := SysErrorMessage(NativeLastError);
      Exit;
    end;
    try
      if not QueryServiceStatus(Service, Status) then begin
        Result := SysErrorMessage(NativeLastError);
        Exit;
      end;
      WasRunning := Status.CurrentState <> ServiceStopped;
      if Running then TargetState := ServiceRunning else TargetState := ServiceStopped;
      RequestSent := False;
      for Attempt := 1 to 120 do begin
        if Status.CurrentState = TargetState then Exit;
        if not RequestSent then begin
          if Running and (Status.CurrentState = ServiceStopped) then begin
            if not StartService(Service, 0, 0) then begin
              Result := SysErrorMessage(NativeLastError);
              Exit;
            end;
            RequestSent := True;
          end else if not Running and (Status.CurrentState <> ServiceStopPending) then begin
            if not ControlService(Service, 1, Status) then begin
              Result := SysErrorMessage(NativeLastError);
              Exit;
            end;
            RequestSent := True;
          end;
        end;
        Sleep(250);
        if not QueryServiceStatus(Service, Status) then begin
          Result := SysErrorMessage(NativeLastError);
          Exit;
        end;
        if Running and RequestSent and (Status.CurrentState = ServiceStopped) then begin
          Result := 'The service stopped during startup. Check the Redfish service log.';
          Exit;
        end;
      end;
      Result := 'Timed out waiting for the Redfish service.';
    finally
      CloseServiceHandle(Service);
    end;
  finally
    CloseServiceHandle(Manager);
  end;
end;

function RunServiceCommand(Command: String): String;
var
  ExitCode: Integer;
begin
  Result := '';
  Log('Redfish service operation: ' + Command);
  if not ExecAndLogOutput(ExpandConstant('{app}\Redfish.exe'), Command + ' --quiet',
    ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ExitCode, nil) then
    Result := 'Could not run Redfish: ' + SysErrorMessage(ExitCode)
  else if ExitCode <> 0 then
    Result := 'Redfish service operation failed (exit code ' + IntToStr(ExitCode) + '). See the setup log for details.';
end;

function InitializeSetup: Boolean;
begin
  Result := IsDotNetInstalled(net48, 0);
  if not Result then
    SuppressibleMsgBox('Redfish requires .NET Framework 4.8 or later. Install it from ' +
      'https://dotnet.microsoft.com/download/dotnet-framework/net48 and run setup again.',
      mbCriticalError, MB_OK, IDOK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  WasRunning: Boolean;
begin
  Result := '';
  if ServiceExists and not OwnsService then begin
    if WizardIsTaskSelected('service') then
      Result := 'A RedfishService service is registered to another application directory. ' +
        'Use that installation directory or leave the service task unchecked.';
    Exit;
  end;
  HadService := OwnsService;
  if HadService then begin
    Result := ChangeServiceState(False, WasRunning);
    RestartService := RestartService or WasRunning;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Error: String;
  Ignored: Boolean;
begin
  if CurStep <> ssPostInstall then Exit;
  if HadService or WizardIsTaskSelected('service') then begin
    Error := RunServiceCommand('--install-service');
    if (Error = '') and RestartService then begin
      Error := ChangeServiceState(True, Ignored);
      if Error = '' then RestartService := False;
    end;
    if Error <> '' then begin
      ServiceSetupFailed := True;
      Log(Error);
      SuppressibleMsgBox(Error + #13#10 + 'The application files are installed, but service setup needs attention.',
        mbCriticalError, MB_OK, IDOK);
    end;
  end;
end;

procedure DeinitializeSetup;
var
  Error: String;
  Ignored: Boolean;
begin
  { Restore a previously running service if setup was canceled or could not finish. }
  if RestartService and OwnsService then begin
    Error := ChangeServiceState(True, Ignored);
    if Error <> '' then Log('Could not restore the running service: ' + Error);
  end;
end;

function GetCustomSetupExitCode: Integer;
begin
  if ServiceSetupFailed then Result := 1 else Result := 0;
end;

function CanLaunchRedfish: Boolean;
begin
  Result := not ServiceSetupFailed;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Error: String;
begin
  { After confirmation and the UI mutex check, but before deleting any application files. }
  if CurUninstallStep <> usUninstall then Exit;
  if not ServiceExists or OwnsService then begin
    Error := RunServiceCommand('--uninstall-service');
    if Error <> '' then begin
      Log(Error);
      SuppressibleMsgBox(Error + #13#10 + 'Uninstall has been canceled to keep the service executable available.',
        mbCriticalError, MB_OK, IDOK);
      Abort;
    end;
  end;
end;
