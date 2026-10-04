param(
    [string] $Configuration = 'Debug'
)

# Run with Windows PowerShell (the app targets .NET Framework).
# Checks entry-point routing and read-only native interop; does not register services or firewall rules.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.ServiceProcess
Add-Type -AssemblyName PresentationFramework
$executable = Join-Path $PSScriptRoot "..\Redfish\bin\$Configuration\Redfish.exe"
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $executable).Path)
if ($assembly.EntryPoint.DeclaringType.FullName -ne 'Redfish.Program') {
    throw 'The executable is not using the combined app/service entry point.'
}
$arguments = [object[]]::new(1)
$arguments[0] = [string[]] @('--unknown-command')
if ($assembly.EntryPoint.Invoke($null, $arguments) -ne 2) {
    throw 'Unknown arguments should exit without opening the UI.'
}
$programType = $assembly.GetType('Redfish.Program', $true)
$commandCheck = $programType.GetMethod('IsServiceCommand', [Reflection.BindingFlags] 'NonPublic,Static')
foreach ($command in @('--install-service', '--uninstall-service', '--start-service', '--stop-service')) {
    $commandArguments = [object[]]::new(1)
    $commandArguments[0] = [string] $command
    if (!$commandCheck.Invoke($null, $commandArguments)) {
        throw "The installer command $command is not recognized."
    }
}
$arguments[0] = [string[]] @('--unknown-command', '--quiet')
if ($assembly.EntryPoint.Invoke($null, $arguments) -ne 2) {
    throw 'Quiet mode should still reject unknown commands without opening the UI.'
}
$arguments[0] = [string[]] @('--install-service', '--unknown-option')
if ($assembly.EntryPoint.Invoke($null, $arguments) -ne 2) {
    throw 'Unexpected options must not execute a service operation.'
}

$service = [Activator]::CreateInstance($assembly.GetType('Redfish.RedfishService', $true))
try {
    if ($service.ServiceName -ne 'RedfishService') {
        throw 'The service runtime name does not match its registration name.'
    }
} finally {
    $service.Dispose()
}

$managerType = $assembly.GetType('Redfish.WindowsServiceManager', $true)
$flags = [Reflection.BindingFlags] 'NonPublic,Static'
$manager = $managerType.GetMethod('OpenSCManager', $flags).Invoke($null, @($null, $null, [uint32] 1))
try {
    if ($manager.IsInvalid) { throw 'Could not open SCM with read-only access.' }
    $missingName = 'Redfish-Smoke-' + [Guid]::NewGuid().ToString('N')
    $missingService = $managerType.GetMethod('OpenService', $flags).Invoke($null, @($manager, $missingName, [uint32] 4))
    try {
        if (!$missingService.IsInvalid) { throw 'Opening a nonexistent service unexpectedly succeeded.' }
    } finally {
        $missingService.Dispose()
    }
} finally {
    $manager.Dispose()
}
Write-Output 'PASS: combined entry point, argument routing, service identity, and read-only SCM interop.'
