# Redfish installer

`Redfish.iss` builds a machine-wide setup executable with Inno Setup 6.4 or later.
It supports x86-compatible Windows, including x64 and ARM64 Windows with emulation support,
and requires .NET Framework 4.8 or later to be installed.
The installer uses 64-bit installation mode on x64-compatible systems, including ARM64 Windows 11.
The setup checks this prerequisite; it does not download or install the framework.

## Build

From the repository root, in a Visual Studio Developer PowerShell:

```powershell
MSBuild.exe Redfish.sln /t:Build /p:Configuration=Release
& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' installer\Redfish.iss
```

On this PC, MSBuild is also available at:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' Redfish.sln /t:Build /p:Configuration=Release
```

The output is `installer\output\RedfishSetup-<version>.exe`. Setup reads the version from
`Redfish.exe`'s file version (currently controlled by `Redfish\Properties\AssemblyInfo.cs`).
Build-directory and version overrides are available:

```powershell
& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' '/DBuildDir=C:\path\to\Release' /DAppVersion=1.2.3 installer\Redfish.iss
```

Keep the script's `AppId` unchanged between releases so Inno recognizes upgrades.
The package contains the executable, its configuration, both SMBLibrary DLLs, and library notices.
It excludes settings, logs, debug symbols, and the old `RedfishService.exe`.

## Install and upgrade behavior

- Installs under Program Files and creates a Start Menu shortcut.
- Offers an optional desktop shortcut and optional Windows service registration, both unchecked on first install.
- Registers the service without starting it on a fresh install. Configure accounts and shares in Redfish first.
- Keeps an existing service registered when upgrading its installation directory, even if the service task is unchecked.
- Stops an existing service before replacing files, and restarts it if it was previously running.
- Attempts to restart that service if setup is canceled after stopping it.
- Requires the UI to be closed before upgrading or uninstalling.
- Leaves settings and old-version data untouched. Settings remain at `%ProgramData%\Redfish\Settings.xml`.

The app's registration command creates the restricted settings directory using the setup account.
If setup is elevated using a different administrator account, explicitly grant the intended UI user
access to the settings directory afterward; setup does not grant all local users access to credentials.

Setup only manages `RedfishService` when its registered executable is this installation's
`Redfish.exe --service`. If the service belongs to another directory or an older separate executable,
selecting the service task is blocked. An installation without the service task can coexist with that
other service. Data and service registration from older versions are not migrated automatically.

Service commands run with `--quiet`; their output and exit codes are included in the setup log.
If service registration fails after files are installed, setup reports the error and returns a nonzero
exit code. The application files remain installed so the service operation can be corrected and retried.

## Uninstall behavior

After confirmation, and before deleting application files, uninstall stops/removes its service
and removes the firewall rule using the application helper. It aborts if that operation fails.
A service registered to another installation directory is left untouched.
Settings are retained; uninstall never recursively removes the application or settings directory.

## Silent setup

Examples, run elevated:

```powershell
.\RedfishSetup-1.0.0.0.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="RedfishSetup.log"
.\RedfishSetup-1.0.0.0.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS="service" /LOG="RedfishSetup.log"
```

Silent setup does not launch the UI. Service registration does not start a fresh, unconfigured service.
Use `/TASKS=""` to explicitly clear previously selected tasks when installing without them.

## Verification

```powershell
powershell.exe -NoProfile -File tests\ServiceSmoke.ps1 -Configuration Release
```

This checks command routing and read-only service interop without modifying machine services.
Before distributing the package, test install, a running-service upgrade, canceled setup,
service-helper failure, and uninstall in a disposable Windows VM. Compilation and smoke checks
do not test those machine-changing operations.

The package is unsigned by default. Configure an Inno `SignTool` and signing certificate when
building signed releases.
