# The closing steps of a change, in the order that works:
#   1. give every changed text file CRLF line endings (keeping its BOM or lack of one)
#   2. build the server in Release
#   3. run the unit tests that need no TIA Portal
#   4. with -Install: stop the installed server and copy the build into Install\TiaMcpServer
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\finish.ps1            # steps 1-3
#   powershell ... -File tools\finish.ps1 -Install                                  # steps 1-4
#
# It does not commit. Commit and push yourself, right after step 4: the client restarts the
# installed server within seconds, and a running server locks Install\TiaMcpServer\TiaMcpServer.exe
# - a later git command that rewrites that file (checkout, merge) would then fail.
#
# Step 4 stops only processes whose exe lies under Install\TiaMcpServer; the user allowed that.
# The tia-mcp-server tools of the running Claude session are gone until the client restarts it.
param([switch]$Install)

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

# --- 1. line endings -------------------------------------------------------------------------
foreach ($file in (git ls-files -m -o --exclude-standard | Where-Object { $_ -match '\.(cs|md|txt|ps1|csproj|json)$' })) {
    $path = Join-Path $root $file
    $bytes = [IO.File]::ReadAllBytes($path)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = [IO.File]::ReadAllText($path)
    $fixed = [regex]::Replace(($text -replace "`r+`n", "`n"), "`n", "`r`n")
    if ($fixed -ne $text) { [IO.File]::WriteAllText($path, $fixed, (New-Object Text.UTF8Encoding($bom))); "line endings fixed: $file" }
}

# --- 2. build --------------------------------------------------------------------------------
$build = dotnet build src\TiaMcpServer\TiaMcpServer.csproj -c Release --nologo -v q 2>&1
$errors = $build | Select-String ' error ' | ForEach-Object { ($_.Line -replace '\[.*$', '').Trim() } | Select-Object -Unique
if ($errors) { 'BUILD FAILED'; $errors; exit 1 }
'build ok'

# --- 3. unit tests ---------------------------------------------------------------------------
# Only the classes marked [TestCategory("NoTia")] run: the others need TIA Portal and the original
# author's projects. A new test class that needs no TIA Portal must carry that category.
$filter = 'TestCategory=NoTia'
$test = dotnet test tests\TiaMcpServer.Test\TiaMcpServer.Test.csproj -c Release --nologo --filter $filter 2>&1
$test | Select-String ' error CS' | Select-Object -First 5 | ForEach-Object { $_.Line }
$summary = $test | Select-Object -Last 1
"tests: $summary"
$failed = $test | Select-String '^\s+(Не пройден|Failed) ' | ForEach-Object { $_.Line.Trim() }
if ($failed) { $failed; 'Run the failing test alone to see its message: dotnet test ... --filter FullyQualifiedName~<name>'; exit 1 }

# --- 4. install ------------------------------------------------------------------------------
if ($Install) {
    Get-CimInstance Win32_Process -Filter "Name='TiaMcpServer.exe'" |
        Where-Object { $_.ExecutablePath -like '*\Install\TiaMcpServer\*' } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -Confirm:$false; "stopped installed server $($_.ProcessId)" }
    Start-Sleep -Milliseconds 700
    Copy-Item src\TiaMcpServer\bin\Release\net48\* Install\TiaMcpServer\ -Recurse -Force
    $same = (Get-FileHash src\TiaMcpServer\bin\Release\net48\TiaMcpServer.exe).Hash -eq (Get-FileHash Install\TiaMcpServer\TiaMcpServer.exe).Hash
    "install updated: $same  - commit and push now"
}
