# Helpers for trying TIA Portal Openness calls directly, before they go into the server.
#
# Why this exists: some ordinary Openness calls close TIA Portal (see docs\handoff\context.md,
# "Calls that close TIA Portal"). A call on a kind of object the server has not touched yet is
# tried here first - one access at a time, a line printed before each attempt, writes inside a
# transaction that is never committed.
#
# Use: dot-source it from a script of your own (keep that script outside the repository):
#
#   . C:\...\MCP_TIA_Portal\tools\openness-probe.ps1
#   Connect-Tia                                  # attaches to the running TIA Portal
#   Save-TestProject                             # so a crash loses nothing
#   $sw = Get-UnifiedSoftware 'HMI Unified'      # HmiSoftware of a device, by device name
#   Probe 'create tag' { $t = $sw.Tags.Create('MCPT_T1'); $t.DataType = 'Real'; "type=$($t.DataType)" }
#   Probe 'read log'   { $sw.DataLogs.Count } -ReadOnly
#
# Every Probe prints "TRY <label>" first, then "OK  <label> -> <result>" or "ERR <label> : [type] text".
# If the output stops after a TRY line, that call closed TIA Portal: restart it with
# tools\start-tia.ps1 and record the call as dangerous.
#
# Reads and writes no files. Changes made inside Probe are rolled back; -ReadOnly skips the
# transaction (use it for reads: opening a transaction makes the project "modified").
#
# PowerShell 5.1 notes: do not name a function or variable 'Try', and do not give a variable the
# name of one of your own parameters in another case ($create vs -Create) - both are the same name.

$script:TiaApi = 'C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48'

foreach ($assembly in 'Base', 'Step7', 'WinCC', 'WinCCUnified') {
    [void][Reflection.Assembly]::LoadFrom("$script:TiaApi\Siemens.Engineering.$assembly.dll")
}

function Out-Line([string]$text) {
    # Console.Out with a flush: the line is on screen before a call that may kill the process.
    [Console]::Out.WriteLine($text)
    [Console]::Out.Flush()
}

function Get-InnerError($errorRecord) {
    $e = $errorRecord.Exception
    while ($e.InnerException) { $e = $e.InnerException }
    "[$($e.GetType().Name)] " + (($e.Message -replace "\r?\n", ' ').Trim())
}

function Connect-Tia {
    $process = [Siemens.Engineering.TiaPortal]::GetProcesses() | Select-Object -First 1
    if (-not $process) { throw 'TIA Portal is not running. Start it with tools\start-tia.ps1.' }
    $script:Tia = $process.Attach()
    if ($script:Tia.Projects.Count -eq 0) { throw 'TIA Portal is running without a project. Open one (tool open_project, or tools\start-tia.ps1 after closing TIA Portal).' }
    $script:Project = $script:Tia.Projects[0]
    Out-Line "attached: project '$($script:Project.Name)', modified=$($script:Project.IsModified)"
}

function Save-TestProject {
    if ($script:Project.IsModified) { $script:Project.Save(); Out-Line 'project saved' }
}

function Get-Service($object, [Type]$serviceType) {
    # GetService<T>() is generic; PowerShell 5.1 cannot call it directly.
    $method = $object.GetType().GetMethods() | Where-Object { $_.Name -eq 'GetService' -and $_.IsGenericMethod } | Select-Object -First 1
    $method.MakeGenericMethod($serviceType).Invoke($object, @())
}

function Invoke-Generic($object, [string]$methodName, [Type]$typeArgument, [object[]]$arguments) {
    # For Create<T>(...) and friends, e.g. Invoke-Generic $screen.ScreenItems 'Create' ([...HmiButton]) @('B1')
    $method = $object.GetType().GetMethods() | Where-Object { $_.Name -eq $methodName -and $_.IsGenericMethod -and $_.GetParameters().Count -eq $arguments.Count } | Select-Object -First 1
    $method.MakeGenericMethod($typeArgument).Invoke($object, $arguments)
}

function Find-Software($deviceItems, [string]$softwareClass) {
    foreach ($item in $deviceItems) {
        $container = Get-Service $item ([Siemens.Engineering.HW.Features.SoftwareContainer])
        if ($container -ne $null -and $container.Software -ne $null -and $container.Software.GetType().Name -eq $softwareClass) { return $container.Software }
        $nested = Find-Software $item.DeviceItems $softwareClass
        if ($nested -ne $null) { return $nested }
    }
    return $null
}

function Get-UnifiedSoftware([string]$deviceName) {
    foreach ($device in $script:Project.Devices) {
        if ($device.Name -eq $deviceName) { return Find-Software $device.DeviceItems 'HmiSoftware' }
    }
    throw "No device '$deviceName'. Devices: $((@($script:Project.Devices) | ForEach-Object Name) -join ', ')"
}

function Get-PlcSoftware([string]$deviceName) {
    foreach ($device in $script:Project.Devices) {
        if ($device.Name -eq $deviceName) { return Find-Software $device.DeviceItems 'PlcSoftware' }
    }
    throw "No device '$deviceName'."
}

function Show-Typed($object, [string[]]$skip = @('Parent')) {
    # The typed .NET properties of an object, read one by one. NEVER use GetAttributeInfos() on
    # a kind of object that has not been checked: on a Unified alarm it closes TIA Portal.
    ($object.GetType().GetProperties() | Where-Object { $_.Name -notin $skip } | ForEach-Object {
        $name = $_.Name
        try { $value = $_.GetValue($object) } catch { $value = '<err>' }
        if ($value -is [Siemens.Engineering.MultilingualText]) { $value = '{' + ((@($value.Items) | ForEach-Object { "$($_.Language.Culture.Name)='$($_.Text)'" }) -join ' ') + '}' }
        "$name=$value"
    }) -join '; '
}

function Probe([string]$label, [scriptblock]$body, [switch]$ReadOnly) {
    Out-Line "TRY $label"

    if (-not $ReadOnly) {
        $access = $script:Tia.ExclusiveAccess('probe')
        $transaction = $access.Transaction($script:Project, 'probe')
    }

    try {
        $result = & $body
        Out-Line "OK  $label -> $result"
    }
    catch {
        Out-Line "ERR $label : $(Get-InnerError $_)"
    }
    finally {
        if (-not $ReadOnly) {
            # Disposing without CommitOnDispose() rolls the transaction back.
            try { $transaction.Dispose(); $access.Dispose() } catch { Out-Line '   dispose failed' }
        }
    }
}
