# Drives a built TiaMcpServer.exe over stdio the way an MCP client does, and prints the answers.
#
# Use: write the calls to a JSON file and run
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\mcp-call.ps1 -Calls calls.json
# The file is an array of { "name": "<tool>", "args": { ... } }; the calls run in order in ONE
# server process, so 'connect' goes first and 'disconnect' last. Build the file in PowerShell
# (see tools\README.md) rather than by hand: ConvertTo-Json gets the escaping right.
#
# Reads: the calls file. Writes: nothing, unless -ToolsOut or -DumpTools name a file.
# The server it starts attaches to the running TIA Portal; a freshly built exe makes TIA Portal
# show an access prompt once, which the user has to confirm.
#
# Output per call:  [n tool] isError=False|True   followed by the result text.
# A call may carry "expect": "text" (the answer must contain it), "expectError": true (the call must be refused) and
# "known": "note" (the check is a defect that is written down, see tools\smoke\README in tools\README.md).
param(
    # JSON file with the calls, or a JSON string.
    [string]$Calls = '[]',
    [string]$Exe = (Join-Path $PSScriptRoot '..\src\TiaMcpServer\bin\Release\net48\TiaMcpServer.exe'),
    # Arguments for the server, e.g. '--read-only' or '--debug-tools'.
    [string]$ExeArgs = '',
    # Longest result text printed per call.
    [int]$Max = 1500,
    [int]$TimeoutSec = 300,
    # Print the result text as the server sent it, without shortening the usual noise.
    [switch]$Raw,
    # Print name(parameters) of the tools whose name matches this regular expression.
    [string]$SchemaFilter = '',
    # Write the sorted tool names to this file (the form of docs\tools-list.txt).
    [string]$ToolsOut = '',
    # Write the full tool definitions, one JSON object per line, to this file.
    [string]$DumpTools = ''
)

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = (Resolve-Path $Exe).Path
$psi.Arguments = $ExeArgs
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true
$psi.StandardOutputEncoding = [Text.Encoding]::UTF8
$p = [System.Diagnostics.Process]::Start($psi)
$stdin = New-Object System.IO.StreamWriter($p.StandardInput.BaseStream, (New-Object Text.UTF8Encoding $false))
$stdin.AutoFlush = $true

function Send($obj) { $stdin.WriteLine(($obj | ConvertTo-Json -Depth 20 -Compress)) }

function Receive($id) {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        $task = $p.StandardOutput.ReadLineAsync()
        while (-not $task.Wait(500)) { if ((Get-Date) -ge $deadline) { return $null } }
        $line = $task.Result
        if ($null -eq $line) { return $null }
        if ($line.Trim().Length -eq 0) { continue }
        try { $msg = $line | ConvertFrom-Json } catch { continue }
        if ($msg.id -eq $id) { return $msg }
    }
    return $null
}

function Shorten([string]$text) {
    if ($Raw) { return $text }
    $text = $text -replace '\\u0027', "'" -replace '\\u003C', '<' -replace '\\u003E', '>' -replace '\\u0022', '"' -replace '\\u002B', '+'
    $text = $text -replace ',"meta":\{[^}]*\}', '' -replace " The change is in memory; call 'SaveProject' to persist it\.", ''
    $text = $text -replace "An error occurred invoking '\w+': ", 'ERROR: ' -replace '"failed":\[\],', ''
    return ($text -replace "\r?\n", ' ')
}

try {
    Send @{ jsonrpc = '2.0'; id = 1; method = 'initialize'; params = @{ protocolVersion = '2025-06-18'; capabilities = @{}; clientInfo = @{ name = 'mcp-call'; version = '0' } } }
    $init = Receive 1
    if ($null -eq $init) { 'NO RESPONSE to initialize'; return }
    Send @{ jsonrpc = '2.0'; method = 'notifications/initialized' }

    Send @{ jsonrpc = '2.0'; id = 2; method = 'tools/list' }
    $tools = Receive 2
    $names = @($tools.result.tools | ForEach-Object { $_.name } | Sort-Object)
    "server: $($init.result.serverInfo.name) $($init.result.serverInfo.version), tools: $($names.Count)"

    if ($DumpTools) { ($tools.result.tools | Sort-Object name | ForEach-Object { $_ | ConvertTo-Json -Depth 30 -Compress }) | Set-Content $DumpTools -Encoding UTF8 }
    if ($ToolsOut) { [IO.File]::WriteAllText($ToolsOut, (($names -join "`r`n") + "`r`n"), (New-Object Text.UTF8Encoding $false)) }

    if ($SchemaFilter) {
        $tools.result.tools | Where-Object { $_.name -match $SchemaFilter } | ForEach-Object {
            $required = @($_.inputSchema.required)
            "{0}({1})" -f $_.name, (($_.inputSchema.properties.PSObject.Properties | ForEach-Object { if ($required -contains $_.Name) { $_.Name } else { "[$($_.Name)=$($_.Value.default)]" } }) -join ', ')
        }
    }

    if (Test-Path $Calls) { $Calls = Get-Content $Calls -Raw -Encoding UTF8 }

    $id = 10
    $number = 0
    foreach ($call in ($Calls | ConvertFrom-Json)) {
        $id++; $number++
        $arguments = @{}
        if ($call.args) { $call.args.PSObject.Properties | ForEach-Object { $arguments[$_.Name] = $_.Value } }

        Send @{ jsonrpc = '2.0'; id = $id; method = 'tools/call'; params = @{ name = $call.name; arguments = $arguments } }
        $r = Receive $id
        if ($null -eq $r) { "[$number $($call.name)] TIMEOUT after $TimeoutSec s"; break }

        $text = if ($r.error) { "JSONRPC ERROR: $($r.error.message)" } else { ($r.result.content | ForEach-Object { $_.text }) -join ' ' }
        $text = Shorten $text
        $isError = [bool]$r.result.isError -or ($null -ne $r.error)

        # Optional checks in the calls file: "expect" is a text the answer has to contain, "expectError": true says the
        # call has to be refused (the header then reads 'isError=True (expected)'). A failed check shows in the header.
        $verdict = "isError=$isError"
        if ($call.expectError -eq $true) {
            $verdict = if ($isError) { 'isError=True (expected)' } else { 'isError=False EXPECTED-ERROR-NOT-RAISED' }
        }
        elseif ($isError) { $verdict = 'isError=True' }
        if ($call.expect) {
            $missing = -not $text.Contains([string]$call.expect)
            # "known": a defect that is written down; the failed check is reported apart, and a check that passes again says so
            if ($call.known -and $missing) { $verdict += " KNOWN-DEFECT: $($call.known)" }
            elseif ($call.known) { $verdict += ' KNOWN-DEFECT-GONE: remove "known" from this call' }
            elseif ($missing) { $verdict += " EXPECT-FAILED: the answer lacks '$($call.expect)'" }
        }

        if ($text.Length -gt $Max) { $text = $text.Substring(0, $Max) + ' ...' }

        "[$number $($call.name)] $verdict"
        "   $text"
    }
}
finally {
    try { $stdin.Close() } catch {}
    if (-not $p.WaitForExit(5000)) { $p.Kill() }
}
