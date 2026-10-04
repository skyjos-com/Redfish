# Running Redfish

The solution builds one executable, `Redfish.exe`, plus its SMBLibrary dependencies.
Both foreground hosting and the Windows service use the same `Settings.xml` beside the executable.

| Command | Behavior |
| --- | --- |
| `Redfish.exe` | Opens the WPF interface. |
| `Redfish.exe --service` | Runs under the Windows Service Control Manager without creating the WPF application. |
| `Redfish.exe --install-service` | Registers or updates `RedfishService` and its inbound TCP firewall rule. Requires administrator rights. |
| `Redfish.exe --uninstall-service` | Stops and removes `RedfishService` and removes its firewall rule. Requires administrator rights. |

The **Run as Service** checkbox launches the registration operation with a UAC elevation prompt,
waits for it to finish, and saves the setting after success. Checking installs the service;
unchecking stops and uninstalls it. Starting and stopping from the main window still require running
the main window as administrator.

Registration uses the quoted absolute path to `Redfish.exe` with `--service`, automatic startup,
and LocalSystem for newly installed services. Updating an existing service preserves its account.
Registration does not start the service. Keep the executable and its dependencies at the registered location.

To migrate an existing installation that points to `RedfishService.exe`, stop the service,
run `Redfish.exe --install-service` as administrator, and then start it again. The service name stays
`RedfishService`; the registered executable path and firewall rule are updated. Existing settings
continue to work when the new executable is deployed in the same directory.

Service installation and firewall updates are separate Windows operations. A failed initial installation
attempt removes the newly created service. An update failure may leave an existing service partly updated;
the application reports the failure and retains the previous checkbox setting, and the operation can be retried.

Build with Visual Studio MSBuild (the WPF project targets .NET Framework 4.8):

```powershell
MSBuild.exe Redfish.sln /t:Build /p:Configuration=Debug
powershell.exe -NoProfile -File tests\ServiceSmoke.ps1
```

The smoke check does not install, remove, start, or stop any Windows service or modify firewall rules.
