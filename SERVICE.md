# Running Redfish

The solution builds one executable, `Redfish.exe`, plus its SMBLibrary dependencies.
Both foreground hosting and the Windows service use `%ProgramData%\Redfish\Settings.xml`.
The file is initialized with empty settings when missing. Files beside the executable are left untouched;
settings from older versions are not migrated.

New settings directories grant Modify access to the user creating them and Full Control to
LocalSystem and Windows administrators, with those permissions inherited by the settings file.
Service installation initializes settings before starting the service, so the installer user can
also edit settings from the UI without elevation. Existing directory permissions are preserved.
If a different Windows user will manage Redfish, or the service runs under a custom account,
an administrator must explicitly grant that account access to this directory.

| Command | Behavior |
| --- | --- |
| `Redfish.exe` | Opens the WPF interface. |
| `Redfish.exe --service` | Runs under the Windows Service Control Manager without creating the WPF application. |
| `Redfish.exe --install-service` | Registers or updates `RedfishService` and its inbound TCP firewall rule. Requires administrator rights. |
| `Redfish.exe --uninstall-service` | Stops and removes `RedfishService` and removes its firewall rule. Requires administrator rights. |
| `Redfish.exe --start-service` | Starts the registered service and waits for it to run. Requires service-control permissions. |
| `Redfish.exe --stop-service` | Stops the registered service, waiting for completion; succeeds if it is absent. Requires service-control permissions. |

Append `--quiet` to service-management commands to suppress dialogs. Failures are written to
standard error and return exit code 1; invalid arguments return exit code 2. The Inno Setup
installer captures this output in its log. See `installer/README.md` for packaging instructions.

The **Run as Service** checkbox launches the registration operation with a UAC elevation prompt,
waits for it to finish, and saves the setting after success. Checking installs the service;
unchecking stops and uninstalls it. Starting and stopping from the main window still require running
the main window as administrator.

Registration uses the quoted absolute path to `Redfish.exe` with `--service`, automatic startup,
and LocalSystem for newly installed services. Updating an existing service preserves its account.
Registration does not start the service. Keep the executable and its dependencies at the registered location.

Service installation and firewall updates are separate Windows operations. A failed initial installation
attempt removes the newly created service. An update failure may leave an existing service partly updated;
the application reports the failure and retains the previous checkbox setting, and the operation can be retried.

Build with Visual Studio MSBuild (the WPF project targets .NET Framework 4.8):

