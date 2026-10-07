# Smoke runs of the built server against the running TIA Portal with the owner's test project open.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\smoke.ps1            # reading
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\smoke.ps1 -Write     # writing, with undo
#   powershell ... -File tools\smoke.ps1 -Calls tools\smoke\read.json -Exe <path to TiaMcpServer.exe>
#
# Reading (tools\smoke\read.json): calls the read-only tools and counts the answers that are errors. After every call
#   it reads the 'isModified' flag of the project (get_project): a read must not change the project, and the first call
#   that does is named. The run needs a saved project: if the project has unsaved changes at the start it prints that
#   and exits with code 2 (it never saves by itself).
#
# Writing (-Write, tools\smoke\write.json): one successful call of every tool that changes the project (the list comes
#   from the built server: tools that '--read-only' leaves out), on objects named MCPT_..., in a temporary PLC station
#   and in WinCC Unified of a panel and of a PC station, and the undo of all of it. Text that Openness can alter
#   silently is read back and compared ("expect" in the calls file, see mcp-call.ps1). Before and after, the inventory
#   (tools\smoke\inventory.json: devices, subnets, connections, PLC counts, screens, tags, lists, alarms, logs, scripts)
#   is read and compared. The project is NOT saved: if an undo failed, nothing reaches the disk. Afterwards the
#   project is marked modified; when the inventory is identical, saving it is harmless.
#   Not in the run: download_to_plc (needs PLCSIM, which the owner starts), sec_protect_project (cannot be undone), retrieve_project (needs no open project) and the project operations
#   save_as_project, close_project, open_project (tools\smoke\project.json, by hand, on a copy).
#   {WORK} in the calls file stands for a temporary folder that the run makes and removes.
#
# Run both after every change of the Siemens\ layer. Exit code: 0 all well, 1 a call failed or the inventory differs,
# 2 the project was not ready (unsaved changes, objects left by an earlier run).
param(
    [string]$Calls = '',
    [string]$Exe = (Join-Path $PSScriptRoot '..\src\TiaMcpServer\bin\Release\net48\TiaMcpServer.exe'),
    [switch]$Write
)

$mcp = Join-Path $PSScriptRoot 'mcp-call.ps1'
$smokeDir = Join-Path $PSScriptRoot 'smoke'
if (-not $Calls) { $Calls = Join-Path $smokeDir $(if ($Write) { 'write.json' } else { 'read.json' }) }

function Invoke-Mcp([string]$callsPath, [int]$max, [string]$exeArgs = '', [string]$toolsOut = '') {
    $p = @{ Calls = $callsPath; Exe = $Exe; Max = $max }
    if ($exeArgs) { $p.ExeArgs = $exeArgs }
    if ($toolsOut) { $p.ToolsOut = $toolsOut }
    @(& $mcp @p 2>&1 | ForEach-Object { "$_" })
}

# The answers of a run as objects: number, tool, verdict (the rest of the header line) and text.
function ConvertTo-Answers($lines) {
    $answers = @()
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\[(\d+) (\S+)\] (.*)$') {
            $text = ''
            if ($i + 1 -lt $lines.Count -and $lines[$i + 1] -like '   *') { $text = $lines[$i + 1].Substring(3) }
            $answers += [pscustomobject]@{ N = [int]$Matches[1]; Tool = $Matches[2]; Verdict = $Matches[3]; Text = $text }
        }
    }
    $answers
}

function Test-Good($verdict) { $verdict -eq 'isError=False' -or $verdict -eq 'isError=True (expected)' }

function Save-Calls($calls, [string]$path) {
    [IO.File]::WriteAllText($path, (ConvertTo-Json -InputObject @($calls) -Depth 30 -Compress), (New-Object Text.UTF8Encoding $false))
}

function Get-Modified([string]$text) {
    if ($text -match '"isModified":(true|false)') { return $Matches[1] -eq 'true' }
    return $null
}

$temp = Join-Path $env:TEMP ('mcpt-smoke-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $temp | Out-Null

try {
    # --- the project must be saved ---------------------------------------------------------------
    $probeFile = Join-Path $temp 'probe.json'
    Save-Calls @(@{ name = 'connect' }, @{ name = 'get_project' }) $probeFile
    $probe = ConvertTo-Answers (Invoke-Mcp $probeFile 20000)
    $state = if ($probe.Count -ge 2) { Get-Modified $probe[1].Text } else { $null }
    if ($null -eq $state) { 'cannot read the state of the project (no project open, or TIA Portal is not reachable):'; $probe | ForEach-Object { "  $($_.Tool): $($_.Verdict) $($_.Text)" }; exit 2 }
    if ($state) { 'The project has unsaved changes. Save it in TIA Portal (or with save_project) and run again: the run never saves by itself.'; exit 2 }

    $parsed = Get-Content $Calls -Raw -Encoding UTF8 | ConvertFrom-Json

    if (-not $Write) {
        # --- reading: a probe after every call ----------------------------------------------------
        $withProbes = @()
        foreach ($call in $parsed) { $withProbes += $call; $withProbes += [pscustomobject]@{ name = 'get_project' } }
        $file = Join-Path $temp 'read.json'
        Save-Calls $withProbes $file
        $answers = ConvertTo-Answers (Invoke-Mcp $file 8000)
        $total = $parsed.Count

        $bad = @(); $answered = 0; $culprit = $null; $previous = $false
        for ($i = 0; $i -lt $answers.Count; $i += 2) {
            $answered++
            $a = $answers[$i]
            $a.N = [int](($a.N + 1) / 2)   # the file sent has a probe after every call
            if (-not (Test-Good $a.Verdict)) { $bad += $a }
            if ($i + 1 -lt $answers.Count) {
                $now = Get-Modified $answers[$i + 1].Text
                if ($now -and -not $previous -and -not $culprit) { $culprit = $a }
                if ($null -ne $now) { $previous = $now }
            }
        }
        if ($answered -lt $total) { "stopped early: $answered of $total calls answered" }
        foreach ($a in $bad) { "[$($a.N) $($a.Tool)] $($a.Verdict)"; "   $($a.Text.Substring(0, [Math]::Min(300, $a.Text.Length)))" }
        if ($culprit) { "the project became modified by a read: [$($culprit.N) $($culprit.Tool)]"; $bad += $culprit }
        "smoke: $answered of $total calls answered, $($bad.Count) with an error; project modified by the run: $(if ($culprit) { 'YES' } else { 'no' })"
        if ($bad.Count -gt 0 -or $answered -lt $total) { exit 1 }
        exit 0
    }

    # --- writing ----------------------------------------------------------------------------------
    # 1. every tool that changes the project has a call in the file
    $all = Join-Path $temp 'all.txt'; $readOnly = Join-Path $temp 'ro.txt'
    Invoke-Mcp '[]' 100 '' $all | Out-Null
    Invoke-Mcp '[]' 100 '--read-only' $readOnly | Out-Null
    $skip = @('download_to_plc', 'sec_protect_project', 'retrieve_project')
    $writeTools = @(Compare-Object (Get-Content $all) (Get-Content $readOnly) | Where-Object { $_.SideIndicator -eq '<=' } | ForEach-Object { $_.InputObject }) | Where-Object { $skip -notcontains $_ }
    $called = @($parsed | ForEach-Object { $_.name })
    $missing = @($writeTools | Where-Object { $called -notcontains $_ })
    if ($missing.Count -gt 0) { "tools that change the project and have no call in $(Split-Path $Calls -Leaf): $($missing -join ', ')" }

    # 2. the inventory before; objects of an earlier run must not be there
    $invFile = Join-Path $smokeDir 'inventory.json'
    $invCalls = (Get-Content $invFile -Raw -Encoding UTF8 | ConvertFrom-Json).Count
    $before = ConvertTo-Answers (Invoke-Mcp $invFile 400000)
    $left = @($before | Where-Object { ($_.Text -replace 'MCPT_Mod', '') -match 'MCPT_' } | ForEach-Object { "[$($_.N) $($_.Tool)]" })
    if ($before.Count -lt $invCalls) { "the inventory stopped early: $($before.Count) of $invCalls calls answered"; exit 2 }
    if ($left.Count -gt 0) { "Objects named MCPT_ are in the project already (an earlier run did not finish its undo): $($left -join ', '). Remove them first."; exit 2 }

    # 3. the run
    $work = Join-Path $temp 'work'; New-Item -ItemType Directory -Path $work | Out-Null
    $text = (Get-Content $Calls -Raw -Encoding UTF8).Replace('{WORK}', ($work -replace '\\', '/'))
    $runFile = Join-Path $temp 'run.json'
    [IO.File]::WriteAllText($runFile, $text, (New-Object Text.UTF8Encoding $false))
    $answers = ConvertTo-Answers (Invoke-Mcp $runFile 1500)

    $bad = @(); $known = @()
    foreach ($a in $answers) {
        if (Test-Good $a.Verdict) { continue }
        if ($a.Verdict -match '^isError=False KNOWN-DEFECT: ') { $known += $a } else { $bad += $a }
    }
    if ($answers.Count -lt $parsed.Count) { "stopped early: $($answers.Count) of $($parsed.Count) calls answered"; $bad += [pscustomobject]@{ N = 0; Tool = 'run'; Verdict = 'stopped early'; Text = '' } }
    foreach ($a in $bad) { "[$($a.N) $($a.Tool)] $($a.Verdict)"; "   $($a.Text.Substring(0, [Math]::Min(400, $a.Text.Length)))" }
    foreach ($a in $known) { "known defect [$($a.N) $($a.Tool)]: $($a.Verdict -replace '^isError=False KNOWN-DEFECT: ', '')" }

    # 4. the inventory after, compared with the one before (times of change are left out)
    $after = ConvertTo-Answers (Invoke-Mcp $invFile 400000)
    $volatile = '"lastModified":"[^"]*"|\d{4}-\d{2}-\d{2}T[\d:.]+Z?'
    $differs = @()
    for ($i = 0; $i -lt [Math]::Min($before.Count, $after.Count); $i++) {
        $x = $before[$i].Text -replace $volatile, ''; $y = $after[$i].Text -replace $volatile, ''
        if ($x -ne $y) {
            $at = 0; while ($at -lt [Math]::Min($x.Length, $y.Length) -and $x[$at] -eq $y[$at]) { $at++ }
            $from = [Math]::Max(0, $at - 60)
            $differs += "inventory [$($before[$i].N) $($before[$i].Tool)] differs at $at : before '...$($x.Substring($from, [Math]::Min(160, $x.Length - $from)))' after '...$($y.Substring($from, [Math]::Min(160, $y.Length - $from)))'"
        }
    }
    if ($after.Count -lt $before.Count) { $differs += "the inventory after the run stopped early: $($after.Count) of $($before.Count)" }
    $differs

    $failed = $bad.Count + $missing.Count + $differs.Count
    "smoke -Write: $($answers.Count) of $($parsed.Count) calls answered, $($bad.Count) failed, $($known.Count) known defect(s), $($missing.Count) tool(s) without a call, inventory: $(if ($differs.Count -eq 0) { 'identical' } else { "$($differs.Count) difference(s)" })"
    if ($failed -eq 0) { 'The project is not saved and is marked modified; the inventory is identical, so saving it is harmless.' }
    else { 'The project is not saved. Look for objects named MCPT_ before saving.' }
    if ($failed -gt 0) { exit 1 }
    exit 0
}
finally {
    Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
}
