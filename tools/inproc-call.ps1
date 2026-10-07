# Calls tools of a built server inside this PowerShell process, without starting TiaMcpServer.exe.
#
# Why: TIA Portal asks the user to grant Openness access to every new build of the server (the allow list goes by the
# hash of the exe), and with TIA Portal minimized that question is not seen - the call just waits. powershell.exe is
# granted once and its hash does not change, so a build loaded into it can be tried against TIA Portal as often as
# needed without a question. Use it while developing; tools\mcp-call.ps1 and tools\smoke.ps1 remain the check of
# the real server over stdio (JSON schema, the gate, the registration), and need the grant.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\inproc-call.ps1 -Calls calls.json
#
# The calls file is the one of mcp-call.ps1: [{ "name": "<tool>", "args": { ... } }, ...]. Arguments are bound by
# parameter name; a parameter that is left out takes its default. Prints one line per call: isError and the answer
# as JSON, cut to -Max characters.
param(
    [Parameter(Mandatory)][string]$Calls,
    [string]$Bin = '',
    [int]$Max = 1500
)

if (-not $Bin) { $Bin = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\src\TiaMcpServer\bin\Release\net48' }

$api = 'C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48'
foreach ($assembly in 'Base', 'Step7', 'WinCC', 'WinCCUnified') { [void][Reflection.Assembly]::LoadFrom("$api\Siemens.Engineering.$assembly.dll") }
$bin = (Resolve-Path $Bin).Path
Add-Type @'
using System; using System.IO; using System.Reflection; using System.Collections.Generic;
public static class InprocResolver {
  static string dir; static readonly Dictionary<string, Assembly> seen = new Dictionary<string, Assembly>();
  public static void Use(string d) { dir = d; AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
    var n = new AssemblyName(e.Name).Name; Assembly a;
    lock (seen) { if (seen.TryGetValue(n, out a)) return a; seen[n] = null; }
    var p = Path.Combine(dir, n + ".dll"); a = File.Exists(p) ? Assembly.LoadFrom(p) : null;
    lock (seen) { seen[n] = a; } return a; }; }
}
'@
[InprocResolver]::Use($bin)
$server = [Reflection.Assembly]::LoadFrom((Join-Path $bin 'TiaMcpServer.exe'))
$mcp = $server.GetType('TiaMcpServer.ModelContextProtocol.McpServer')
$tools = @{}
foreach ($m in $mcp.GetMethods([Reflection.BindingFlags]'Public,Static')) {
    $a = $m.GetCustomAttributes($false) | Where-Object { $_.GetType().Name -eq 'McpServerToolAttribute' } | Select-Object -First 1
    if ($a -and $a.Name) { $tools[$a.Name] = $m }
}
$json = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq 'System.Text.Json' } | Select-Object -First 1
if (-not $json) { $json = [Reflection.Assembly]::LoadFrom((Join-Path $bin 'System.Text.Json.dll')) }
$serializer = $json.GetType('System.Text.Json.JsonSerializer')
$options = [Activator]::CreateInstance($json.GetType('System.Text.Json.JsonSerializerOptions'))
$options.PropertyNameCaseInsensitive = $true
$options.PropertyNamingPolicy = $json.GetType('System.Text.Json.JsonNamingPolicy').GetProperty('CamelCase').GetValue($null)
$options.DefaultIgnoreCondition = [Enum]::Parse($json.GetType('System.Text.Json.Serialization.JsonIgnoreCondition'), 'WhenWritingNull')
$deserialize = $serializer.GetMethods() | Where-Object { $_.Name -eq 'Deserialize' -and -not $_.IsGenericMethod -and $_.GetParameters().Count -eq 3 -and $_.GetParameters()[0].ParameterType -eq [string] } | Select-Object -First 1
$serialize = $serializer.GetMethods() | Where-Object { $_.Name -eq 'Serialize' -and -not $_.IsGenericMethod -and $_.GetParameters().Count -eq 3 -and $_.GetParameters()[0].ParameterType -eq [object] -and $_.ReturnType -eq [string] } | Select-Object -First 1

$text = if (Test-Path $Calls) { Get-Content $Calls -Raw -Encoding UTF8 } else { $Calls }
"in process: $($tools.Count) tools of $bin"
$n = 0
foreach ($call in ($text | ConvertFrom-Json)) {
    $n++
    $method = $tools[$call.name]
    if (-not $method) { "[$n $($call.name)] no such tool"; continue }
    $arguments = @()
    foreach ($p in $method.GetParameters()) {
        $given = if ($call.args) { $call.args.PSObject.Properties[$p.Name] } else { $null }
        if ($given) { $arguments += , $deserialize.Invoke($null, @([string](ConvertTo-Json -InputObject $given.Value -Depth 30 -Compress), $p.ParameterType, $options)) }
        elseif ($p.HasDefaultValue) { $arguments += , $p.DefaultValue }
        else { $arguments += , $null }
    }
    try {
        $result = $method.Invoke($null, $arguments)
        if ($result -is [Threading.Tasks.Task]) { $result.Wait(); $result = $result.GetType().GetProperty('Result').GetValue($result) }
        $out = $serialize.Invoke($null, @($result, $result.GetType(), $options))
        "[$n $($call.name)] isError=False"
    } catch {
        # the message a client gets is the one of the tool (McpException), not of the cause below it
        $e = $_.Exception; while ($e.InnerException -and $e.GetType().Name -notmatch '^(McpException|PortalException)$') { $e = $e.InnerException }
        $out = 'ERROR: ' + $e.Message
        "[$n $($call.name)] isError=True"
    }
    '   ' + $(if ($out.Length -gt $Max) { $out.Substring(0, $Max) + ' ...' } else { $out })
}
