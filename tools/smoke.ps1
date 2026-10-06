# Smoke run: calls the read-only tools of the built server against the running TIA Portal with the
# test project open, and reports how many calls the server answered with an error.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\smoke.ps1
#   powershell ... -File tools\smoke.ps1 -Calls tools\smoke\read.json -Exe <path to TiaMcpServer.exe>
#
# Run it after every build that changed the Siemens\ layer. It reads only: no call changes the
# project. The names in tools\smoke\read.json are those of the owner's test project (see
# docs\handoff\context.md); for another project edit the file. Exit code 1 when a call failed.
param(
    [string]$Calls = (Join-Path $PSScriptRoot 'smoke\read.json'),
    [string]$Exe = (Join-Path $PSScriptRoot '..\src\TiaMcpServer\bin\Release\net48\TiaMcpServer.exe')
)

$output = & (Join-Path $PSScriptRoot 'mcp-call.ps1') -Calls $Calls -Exe $Exe -Max 300 2>&1 | ForEach-Object { "$_" }
$heads = @($output | Where-Object { $_ -match '^\[\d+ ' })
$bad = @($heads | Where-Object { $_ -notmatch 'isError=False$' })
$parsed = Get-Content $Calls -Raw -Encoding UTF8 | ConvertFrom-Json
$total = $parsed.Count

if ($heads.Count -lt $total) { "stopped early: $($heads.Count) of $total calls answered"; $output | Select-Object -Last 3 }

foreach ($line in $bad) {
    $line
    $index = [array]::IndexOf($output, $line)
    if ($index -ge 0 -and $index + 1 -lt $output.Count) { $output[$index + 1] }
}

"smoke: $($heads.Count) of $total calls answered, $($bad.Count) with an error"
if ($bad.Count -gt 0 -or $heads.Count -lt $total) { exit 1 }
