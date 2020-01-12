@echo off

set exe_file_path=%cd%\RedfishService.exe

REM Delete firewall rule
netsh advfirewall firewall Delete rule name="RedfishService"

REM Add firewall rule
netsh advfirewall firewall add rule name="RedfishService" dir=in action=allow program=%exe_file_path% enable=yes

REM install service
%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\installutil.exe RedfishService.exe

REM Net Start RedfishService
REM sc config RedfishService start= auto

::pause