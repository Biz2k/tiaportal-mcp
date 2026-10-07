# Finds out how TIA Portal writes LAD instructions in a SIMATIC SD document: the name, the pins, what the compile wants.
# There is no list of these names anywhere - not in the help, not in the installation - but the import answers:
#   "Instruction 'Sr' : Pin 'r1' connection is missing"  - the name is known, and this pin is required;
#   "Not a Valid instruction: X"                         - it is not;
#   success with the text unchanged and a compile error "X_Callee no longer exists" - taken for a call of a block X.
# For every candidate the script imports "Name( pins )" into a temporary LAD block MCPT_Ins, adds the pin reported as
# missing, repeats until the import passes, and reads the canonical text back from the export. Names of the help
# (SCALE_X, SR) are accepted as input and come back in their own spelling (Scale, S_SR).
#
#   -List    text file, one candidate per line; a second tab-separated column is a leading operand (#b for a contact or coil)
#   -Out     result, tab-separated: candidate, verdict, internal name, steps, text sent, text returned or error, compile
#   -Device  name of the device whose PLC is used; about 5 seconds per candidate
# The result of 2026-10-07 (TIA Portal V21, S7-1500) is docs/handoff/api/lad-instructions.tsv.
param([Parameter(Mandatory)][string]$List, [Parameter(Mandatory)][string]$Out, [Parameter(Mandatory)][string]$Device, [string]$Work = (Join-Path $env:TEMP 'lad-instruction-probe'))
. (Join-Path $PSScriptRoot 'openness-probe.ps1')
Connect-Tia
$dir = Join-Path $Work 'in'; $back = Join-Path $Work 'back'
$sw = Get-PlcSoftware $Device
$utf8 = New-Object System.Text.UTF8Encoding $true
$head = "{`r`n    S7_Optimized := `"TRUE`";`r`n    S7_PreferredLanguage := `"LAD`";`r`n    S7_Version := `"0.1`"`r`n}`r`nFUNCTION_BLOCK `"MCPT_Ins`"`r`n    VAR`r`n        b : Bool;`r`n        m : Bool;`r`n        i : Int;`r`n    END_VAR`r`n`r`n    { S7_Language := `"LAD`" }`r`n    NETWORK`r`n        RUNG wire#powerrail`r`n"
$tail = "`r`n        END_RUNG`r`n    END_NETWORK`r`nEND_FUNCTION_BLOCK`r`n"
function Try-Line([string]$line) {
  foreach ($d in @($dir, $back)) { if (Test-Path $d) { Remove-Item $d -Recurse -Force }; New-Item -ItemType Directory $d -Force | Out-Null }
  [IO.File]::WriteAllText((Join-Path $dir 'MCPT_Ins.s7dcl'), ($head + '            ' + $line + $tail), $utf8)
  try {
    $r = $sw.BlockGroup.Blocks.ImportFromDocuments((New-Object IO.DirectoryInfo $dir), 'MCPT_Ins', [Siemens.Engineering.SW.ImportDocumentOptions]::Override)
    if ([string]$r.State -eq 'Success') {
      $b = $sw.BlockGroup.Blocks.Find('MCPT_Ins')
      [void]$b.ExportAsDocuments((New-Object IO.DirectoryInfo $back), 'MCPT_Ins')
      $t = [IO.File]::ReadAllText((Join-Path $back 'MCPT_Ins.s7dcl')) -replace "`r", ''
      $m = [regex]::Match($t, '(?s)RUNG wire#powerrail\n(.*?)\n\s*END_RUNG')
      $txt = (($m.Groups[1].Value -split "`n" | ForEach-Object { $_.Trim() }) -join ' ')
      $script:compileNote = ''
      try { $c = (Get-Service $b ([Siemens.Engineering.Compiler.ICompilable])).Compile(); $script:compileNote = [string]$c.State; if ($c.ErrorCount -gt 0) { $q = New-Object System.Collections.Queue; foreach ($x in $c.Messages) { $q.Enqueue($x) }; $errs = @(); while ($q.Count -gt 0) { $x = $q.Dequeue(); if ($x.Description -and [string]$x.State -eq 'Error' -and $x.Description -notlike 'Compiling finished*') { $errs += $x.Description }; foreach ($y in $x.Messages) { $q.Enqueue($y) } }; $script:compileNote = 'Error: ' + (($errs | Select-Object -First 2) -join ' / ') } } catch { $script:compileNote = 'compile failed' }
      return @('OK', $txt)
    }
    $e = ((@($r.Messages | ForEach-Object { $_.Message } | Where-Object { $_ -like 'Error*' }) -join ' / ') -replace '\s+', ' ') -replace ' Error in (Line Number: \d+, in )?Network:.*', ''
    return @('REFUSED', $e)
  } catch { return @('EXCEPTION', ((Get-InnerError $_) -replace '\s+', ' ')) }
}
$n = 0
[IO.File]::WriteAllText($Out, '', $utf8)
foreach ($row in [IO.File]::ReadAllLines($List)) {
  $cand = ($row -split "`t")[0].Trim()
  if (-not $cand) { continue }
  $operand = ($row -split "`t")[1]
  $pins = New-Object System.Collections.Specialized.OrderedDictionary
  $internal = ''; $verdict = ''; $text = ''; $steps = 0
  while ($steps -lt 70) {
    $steps++
    $args = @(); if ($operand) { $args += $operand }
    foreach ($k in $pins.Keys) { $args += ($k + ' ' + $pins[$k] + ' #i') }
    $line = $cand + '( ' + ($args -join ', ') + ' )'
    $res = Try-Line $line
    $verdict = $res[0]; $text = $res[1]
    if ($verdict -ne 'REFUSED') { break }
    $m = [regex]::Match($text, "Instruction '([^']+)' : Pin '([^']+)' connection is missing")
    if (-not $m.Success) { break }
    $internal = $m.Groups[1].Value; $pin = $m.Groups[2].Value
    if (-not $pins.Contains($pin)) { $pins[$pin] = $(if ($pin -match '^(outd*|ret_val)$') { '=>' } else { ':=' }) }
    elseif ($pins[$pin] -eq ':=') { $pins[$pin] = '=>' }
    else { $verdict = 'STUCK'; break }
  }
  $n++
  [IO.File]::AppendAllText($Out, ($cand + "`t" + $verdict + "`t" + $internal + "`t" + $steps + "`t" + $line + "`t" + $text + "`t" + $(if ($verdict -eq 'OK') { $script:compileNote } else { '' }) + "`r`n"), $utf8)
}
$b = $sw.BlockGroup.Blocks.Find('MCPT_Ins'); if ($b -ne $null) { $b.Delete() }
Out-Line ("done: " + $n)
