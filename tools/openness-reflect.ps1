# Lists what TIA Portal Openness offers in a namespace: classes with their properties
# (a trailing * marks a writable one) and methods, and enums with their members.
#
# Use this BEFORE deciding how to build a tool, and before telling anyone that Openness cannot do
# something - what you need is often in another namespace than the class at hand (an integrated
# HMI connection is made in Siemens.Engineering.HW, not in HmiUnified.HmiConnections).
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\openness-reflect.ps1 -Namespace 'HmiUnified.Scripts'
#   powershell ... -File tools\openness-reflect.ps1 -Name 'Connection'       # every public type with that in its name
#   powershell ... -File tools\openness-reflect.ps1 -Namespace 'HmiUnified.HmiLogging' -Out docs\handoff\api\logging.txt
#
# Reads only the Openness assemblies; it does not need TIA Portal to run and cannot harm a project.
param(
    # Part of a namespace, matched as 'Siemens.Engineering.*<Namespace>*'.
    [string]$Namespace = '',
    # Part of a type name; lists full names only.
    [string]$Name = '',
    [string]$Out = '',
    [string]$Api = 'C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48'
)

$types = @()
foreach ($assembly in 'Base', 'Step7', 'WinCC', 'WinCCUnified') {
    $loaded = [Reflection.Assembly]::LoadFrom("$Api\Siemens.Engineering.$assembly.dll")
    # Some types fail to load without their product; the rest are still usable.
    try { $types += $loaded.GetTypes() } catch { $types += $_.Exception.InnerException.Types | Where-Object { $_ } }
}

$common = '^(GetAttribute|GetAttributes|GetAttributeInfos|SetAttribute|SetAttributes|Equals|GetHashCode|ToString|GetType|GetEnumerator|Any|Contains|IndexOf)$'
$lines = New-Object System.Collections.Generic.List[string]

if ($Name) {
    $types | Where-Object { $_.IsPublic -and $_.Name -match $Name -and $_.Name -notmatch 'FactoryFacade' } | ForEach-Object { $_.FullName } | Sort-Object | ForEach-Object { $lines.Add($_) }
}

if ($Namespace) {
    foreach ($type in ($types | Where-Object { $_.IsPublic -and $_.Namespace -like "Siemens.Engineering.*$Namespace*" -and $_.Name -notmatch 'FactoryFacade' } | Sort-Object FullName)) {
        if ($type.IsEnum) {
            $lines.Add("ENUM $($type.FullName): " + ([Enum]::GetNames($type) -join ', '))
            continue
        }

        $lines.Add("CLASS $($type.FullName) : $($type.BaseType.Name)")
        $properties = ($type.GetProperties() | ForEach-Object { "$($_.Name):$($_.PropertyType.Name)$(if ($_.CanWrite) { '*' })" }) -join ', '
        if ($properties) { $lines.Add("   props: $properties") }

        $methods = ($type.GetMethods() | Where-Object { -not $_.IsSpecialName -and $_.Name -notmatch $common -and $_.DeclaringType.Namespace -like 'Siemens*' } | ForEach-Object {
            "$($_.Name)$(if ($_.IsGenericMethod) { '<T>' })(" + (($_.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', ') + ")"
        }) -join '; '
        if ($methods) { $lines.Add("   methods: $methods") }
    }
}

if ($Out) { [IO.File]::WriteAllText((Join-Path (Get-Location) $Out), (($lines -join "`r`n") + "`r`n"), (New-Object Text.UTF8Encoding $false)); "$($lines.Count) lines -> $Out" }
else { $lines }
