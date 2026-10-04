param([string] $Configuration = 'Debug')

# Uses a temporary directory; does not access or change the machine's Redfish settings.
$ErrorActionPreference = 'Stop'
$executable = Join-Path $PSScriptRoot "..\Redfish\bin\$Configuration\Redfish.exe"
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $executable).Path)
$method = $assembly.GetType('Redfish.SettingsHelper', $true).GetMethod(
    'InitializeSettingsFile', [Reflection.BindingFlags] 'NonPublic,Static')
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('RedfishSettingsTest-' + [Guid]::NewGuid().ToString('N'))
$settingsDirectory = Join-Path $temporaryRoot 'Redfish'
$expectedPath = Join-Path $settingsDirectory 'Settings.xml'
$initializationArguments = [object[]]::new(1)
$initializationArguments[0] = [string] $settingsDirectory
try {
    $actualPath = $method.Invoke($null, $initializationArguments)
    if ($actualPath -ne $expectedPath) { throw 'Settings path is incorrect.' }
    $document = [xml] [IO.File]::ReadAllText($actualPath)
    if ($document.DocumentElement.Name -ne 'Settings' -or $document.DocumentElement.ChildNodes.Count -ne 0) {
        throw 'Fresh settings should contain an empty Settings element.'
    }

    $security = [IO.Directory]::GetAccessControl($settingsDirectory)
    if (!$security.AreAccessRulesProtected) { throw 'Settings directory inherits unrelated permissions.' }
    $rules = $security.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier])
    $currentSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    foreach ($sid in @($currentSid, 'S-1-5-18', 'S-1-5-32-544')) {
        if (!($rules | Where-Object { $_.IdentityReference.Value -eq $sid -and $_.AccessControlType -eq 'Allow' })) {
            throw "Missing settings permission for $sid."
        }
    }
    foreach ($rule in $rules) {
        if ($rule.IdentityReference.Value -notin @($currentSid, 'S-1-5-18', 'S-1-5-32-544')) {
            throw 'Settings directory grants access to an unrelated account.'
        }
    }
    $fileRules = [IO.File]::GetAccessControl($actualPath).GetAccessRules(
        $true, $true, [Security.Principal.SecurityIdentifier])
    foreach ($sid in @($currentSid, 'S-1-5-18', 'S-1-5-32-544')) {
        if (!($fileRules | Where-Object { $_.IdentityReference.Value -eq $sid -and $_.AccessControlType -eq 'Allow' })) {
            throw "Settings file did not inherit permission for $sid."
        }
    }
    foreach ($rule in $fileRules) {
        if ($rule.IdentityReference.Value -notin @($currentSid, 'S-1-5-18', 'S-1-5-32-544')) {
            throw 'Settings file grants access to an unrelated account.'
        }
    }

    $existingSettings = '<Settings><Port Number="25445" /></Settings>'
    [IO.File]::WriteAllText($actualPath, $existingSettings)
    $null = $method.Invoke($null, $initializationArguments)
    if ([IO.File]::ReadAllText($actualPath) -cne $existingSettings) {
        throw 'Existing settings were replaced.'
    }
    if (@(Get-ChildItem -LiteralPath $settingsDirectory -Filter '*.tmp').Count -ne 0) {
        throw 'Settings initialization left temporary files behind.'
    }
    Write-Output 'PASS: empty settings, existing-file preservation, restricted permissions, and temporary-file cleanup.'
} finally {
    $resolvedRoot = [IO.Path]::GetFullPath($temporaryRoot)
    $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolvedRoot.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedRoot) -notlike 'RedfishSettingsTest-*') {
        throw 'Refusing to remove an unexpected test directory.'
    }
    if (Test-Path -LiteralPath $resolvedRoot) {
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
