# Starts TIA Portal with a project, as a user would, and waits until Openness sees the project.
# Use after TIA Portal was closed by a crash. Does nothing if an instance already has a project.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\start-tia.ps1
#   powershell ... -File tools\start-tia.ps1 -Project 'D:\Projects\Other\Other.ap21'
#
# The default project is the user's TEST project, in which anything may be changed and saved.
# Do not point this at another project without the user's word.
param(
    [string]$Project = 'C:\Users\Biz\Desktop\21474_SEVGOK_P2_v4_V21_18_26\21474_SEVGOK_P2_v4_V21.ap21',
    [string]$Exe = 'C:\Program Files\Siemens\Automation\Portal V21\Bin\Siemens.Automation.Portal.exe',
    [string]$Api = 'C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48'
)

[void][Reflection.Assembly]::LoadFrom("$Api\Siemens.Engineering.Base.dll")

$running = [Siemens.Engineering.TiaPortal]::GetProcesses() | Select-Object -First 1
if ($running -and $running.ProjectPath) { "already running with $($running.ProjectPath.Name)"; return }

if (-not (Test-Path $Project)) { throw "Project not found: $Project" }

Start-Process -FilePath $Exe -ArgumentList "`"$Project`""

for ($i = 1; $i -le 60; $i++) {
    Start-Sleep -Seconds 10
    $process = [Siemens.Engineering.TiaPortal]::GetProcesses() | Select-Object -First 1
    if ($process -and $process.ProjectPath) { "project loaded after $($i * 10) s: $($process.ProjectPath.Name)"; return }
}

'TIA Portal did not report a project within 10 minutes'
