@echo off

REM Delete firewall rule
netsh advfirewall firewall Delete rule name="RedfishService"

REM uninstall service
%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\installutil.exe /u RedfishService.exe

::pause