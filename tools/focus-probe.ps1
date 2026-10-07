param([string]$Label = 'run', [switch]$Minimize, [int]$Rounds = 6, [string]$Device = 'ET 200SP station_1', [string]$SoftwarePath = '' + $SoftwarePath + '', [string]$Block = 'Main')
# Measures whether TIA Portal takes the keyboard focus during Openness calls (docs/PLAN.md, 4.6).
# A second process samples the foreground window every 15 ms; this one puts the Claude window in front before each
# step - exclusive access, compile, a write through the server - and marks the steps in the same log; the summary
# counts the steps during which a window of TIA Portal came to the front. The screen flickers while it runs: use it
# on a PC nobody is working on. Creates and deletes a tag table MCPT_FG; saves the project at the end.
$s = Join-Path $env:TEMP "tia-focus-probe"; New-Item -ItemType Directory $s -Force | Out-Null
$log = Join-Path $s 'fgm.log'; $stop = Join-Path $s 'fgm.stop'
foreach ($f in @($log, $stop)) { if (Test-Path $f) { Remove-Item $f } }
$watch = @'
Add-Type 'using System; using System.Runtime.InteropServices; public static class W { [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow(); [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid); }'
$names = @{}; $last = ''
while (-not (Test-Path '__STOP__')) {
  $h = [W]::GetForegroundWindow(); $p = 0; [void][W]::GetWindowThreadProcessId($h, [ref]$p)
  if (-not $names.ContainsKey($p)) { try { $names[$p] = (Get-Process -Id $p -ErrorAction Stop).ProcessName } catch { $names[$p] = '?' } }
  $n = $names[$p]
  if ($n -ne $last) { [IO.File]::AppendAllText('__LOG__', ((Get-Date).ToString('HH:mm:ss.fff') + ' FG ' + $n + "`r`n")); $last = $n }
  Start-Sleep -Milliseconds 15
}
'@.Replace('__LOG__', $log).Replace('__STOP__', $stop)
$wf = Join-Path $s 'fgmwatch.ps1'; [IO.File]::WriteAllText($wf, $watch)
. C:\Users\Biz\Antigravity\MCP_TIA_Portal\tools\openness-probe.ps1
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices;
public static class Fg {
  delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  public static IntPtr MainOf(int pid) { IntPtr found = IntPtr.Zero; EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) { var s = new StringBuilder(300); GetWindowText(h, s, 300); if (s.Length > 10) { found = h; return false; } } return true; }, IntPtr.Zero); return found; }
}
'@
$claude = Get-Process | Where-Object { $_.ProcessName -like 'claude*' -and $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$ws = New-Object -ComObject WScript.Shell
function Mark([string]$t) { [IO.File]::AppendAllText($log, ((Get-Date).ToString('HH:mm:ss.fff') + ' ' + $t + "`r`n")) }
function Front { if (-not [Fg]::SetForegroundWindow($claude.MainWindowHandle)) { [void]$ws.AppActivate($claude.Id) }; Start-Sleep -Milliseconds 900 }
function Run-Step([string]$name, [scriptblock]$body) { Front; Mark "STEP $name"; try { & $body } catch { Mark ("ERR " + (Get-InnerError $_)) }; Start-Sleep -Milliseconds 700; Mark 'END' }
Connect-Tia
$tp = [Siemens.Engineering.TiaPortal]::GetProcesses() | Select-Object -First 1
$parent = ''; try { $parent = (Get-Process -Id (Get-CimInstance Win32_Process -Filter "ProcessId=$($tp.Id)").ParentProcessId -ErrorAction Stop).ProcessName } catch { $parent = '(gone)' }
$main = [Fg]::MainOf($tp.Id)
if ($Minimize) { [void][Fg]::ShowWindow($main, 6) } else { [void][Fg]::ShowWindow($main, 4) }
Start-Sleep -Milliseconds 800
$w = Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden', '-File', ('"' + $wf + '"') -PassThru -WindowStyle Hidden
Start-Sleep -Milliseconds 2500
$sw = Get-PlcSoftware $Device
$cf = Join-Path $s 'fgcalls.json'
[IO.File]::WriteAllText($cf, '[{"name":"connect","args":{}},{"name":"plc_create_tag_table","args":{"softwarePath":"' + $SoftwarePath + '","groupPath":"","name":"MCPT_FG"}},{"name":"plc_delete_tag_table","args":{"softwarePath":"' + $SoftwarePath + '","tagTablePath":"MCPT_FG"}}]')
1..$Rounds | ForEach-Object {
  Run-Step 'probe: exclusive access' { $a = $script:Tia.ExclusiveAccess('focus probe'); Start-Sleep -Milliseconds 800; $a.Dispose() }
  Run-Step 'probe: compile block' { $c = (Get-Service ($sw.BlockGroup.Blocks.Find($Block)) ([Siemens.Engineering.Compiler.ICompilable])).Compile() }
  Run-Step 'server: create + delete table' { $null = & powershell -NoProfile -ExecutionPolicy Bypass -File C:\Users\Biz\Antigravity\MCP_TIA_Portal\tools\mcp-call.ps1 -Calls $cf }
}
New-Item $stop -ItemType File | Out-Null
Start-Sleep -Milliseconds 600
# summary
$lines = [IO.File]::ReadAllLines($log)
"== $Label : TIA pid $($tp.Id), started $((Get-Process -Id $tp.Id).StartTime.ToString('HH:mm')), parent $parent, window minimized=$([Fg]::IsIconic($main))"
$step = $null; $seen = @(); $count = [ordered]@{}; $taken = @{}; $back = @{}
foreach ($l in $lines) {
  if ($l -match ' STEP (.+)$') { $step = $Matches[1]; $seen = @() }
  elseif ($l -match ' FG (.+)$') { if ($step) { $seen += $Matches[1] } }
  elseif ($l -match ' ERR (.+)$') { "   error: $($Matches[1])" }
  elseif ($l -match ' END$' -and $step) {
    $count[$step] = 1 + [int]$count[$step]
    if ($seen -match 'Siemens') { $taken[$step] = 1 + [int]$taken[$step]; if ($seen[-1] -notmatch 'Siemens') { $back[$step] = 1 + [int]$back[$step] } }
    $step = $null
  }
}
foreach ($k in $count.Keys) { ('   {0,-34} focus taken {1} of {2}{3}' -f $k, [int]$taken[$k], $count[$k], $(if ($back[$k]) { " (came back by itself $($back[$k]))" } else { '' })) }
if ($script:Project.IsModified) { $script:Project.Save(); '   project saved' }
Front
