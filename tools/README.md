# Development scripts

PowerShell 5.1 scripts used while developing the server. None of them is part of the server.
Run them from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\<script>.ps1 <arguments>
```

| Script | What it does | Touches TIA Portal |
|---|---|---|
| `openness-reflect.ps1` | Lists the classes, properties, methods and enums Openness has in a namespace | no - reads the assemblies only |
| `openness-probe.ps1` | Helpers to try Openness calls one at a time, writes rolled back | yes - attaches to the running instance |
| `make-tool-docs.ps1` | Writes `docs/tools/*.md` from the tool definitions of the built server | no - only `tools/list` |
| `mcp-call.ps1` | Runs tool calls against a built server over stdio, as an MCP client would | yes - through the server |
| `smoke.ps1` | Calls the read-only tools (`tools\smokeead.json`) on the test project and counts the errors | yes - reads only |
| `finish.ps1` | Line endings, build, unit tests; with `-Install` updates `Install\TiaMcpServer` | no |
| `start-tia.ps1` | Starts TIA Portal with the test project and waits for it | starts it |

## The order they are used in

1. **Look** - `openness-reflect.ps1 -Namespace 'HmiUnified.HmiLogging'` shows what exists.
   `-Name 'Connection'` finds types by name across all assemblies; do that before concluding
   that Openness cannot do something.
2. **Try** - a script of your own that dot-sources `openness-probe.ps1` shows how the calls
   behave: what is accepted silently, what order properties need, what closes TIA Portal.
3. **Build and test** - `finish.ps1`.
4. **Check the server** - `mcp-call.ps1` with a calls file.
5. **Publish** - `finish.ps1 -Install`, then commit and push at once.

## openness-probe.ps1

```powershell
. C:\Users\Biz\Antigravity\MCP_TIA_Portal\tools\openness-probe.ps1
Connect-Tia
Save-TestProject
$sw = Get-UnifiedSoftware 'HMI Unified'

Probe 'counts' { "dataLogs=$($sw.DataLogs.Count)" } -ReadOnly
Probe 'create tag' { $t = $sw.Tags.Create('MCPT_T1'); $t.DataType = 'Real'; Show-Typed $t }
```

- `Probe` prints `TRY <label>` before the call and `OK` or `ERR` after it. Output that stops
  after `TRY` means the call closed TIA Portal.
- Without `-ReadOnly` the body runs in a transaction that is rolled back.
- `Show-Typed $object` reads the typed .NET properties. Do not call `GetAttributeInfos()` on a
  kind of object that has not been checked - on a WinCC Unified alarm it closes TIA Portal.
- `Get-Service $item ([Type])` and `Invoke-Generic $composition 'Create' ([Type]) @(args)`
  call the generic Openness methods PowerShell 5.1 cannot call directly.
- Keep probe scripts outside the repository (the session scratchpad). A probe script with
  non-ASCII text in it has to be saved as UTF-8 with BOM.
- One risky call per run, the riskiest last, the test project saved first.

## mcp-call.ps1

The calls file is a JSON array of `{ "name": "<tool>", "args": { ... } }`. All calls run in one
server process, so `connect` (or `open_tia_project`) goes first.

```powershell
$h = 'HMI Unified/HMI_RT_3'
$calls = @(
  @{ name = 'connect' },
  @{ name = 'unified_get_tag_tables'; args = @{ softwarePath = $h } },
  @{ name = 'disconnect' })
$calls | ConvertTo-Json -Depth 10 | Set-Content "$scratch\calls.json" -Encoding UTF8
powershell -NoProfile -ExecutionPolicy Bypass -File tools\mcp-call.ps1 -Calls "$scratch\calls.json"
```

- A one-element array needs the comma form: `actions = @(,@{ action = 'create'; ... })`.
- `-Max 5000` prints longer results; `-Raw` leaves the text as the server sent it.
- `-SchemaFilter '^unified_'` prints the parameters of the matching tools.
- `-ToolsOut names.txt` writes the tool names in the form of `docs\tools-list.txt`;
  `-DumpTools tools.jsonl` writes the full definitions.
- `-ExeArgs '--read-only'` or `'--debug-tools'` passes flags to the server.
- A newly built exe makes TIA Portal ask for Openness access once; the user confirms it.

## smoke.ps1

Runs 48 read-only calls of the built server on the test project (the project tree, the PLC `PLC (A0)`, the panel
`HMI Unified/HMI_RT_3` and the PC station `АРМ Unified/HMI_RT_1`) and prints `smoke: N of N calls answered, M with an error`.
Exit code 1 when a call fails or the server stops answering. Run it after every build that changed the `Siemens\` layer,
before `finish.ps1 -Install`. The calls file is UTF-8 (the station name is Cyrillic); a new read tool gets a line in
`tools\smokeead.json`. TIA Portal has to be running with the test project (`start-tia.ps1`).

## finish.ps1

Runs the unit tests that need no TIA Portal: the classes marked `[TestCategory("NoTia")]`
(`dotnet test --filter TestCategory=NoTia`). A new test class that needs no TIA Portal must carry
that category, or it is not run.

`-Install` stops the installed server (only processes under `Install\TiaMcpServer`) and copies
the build there. Commit right after it: the MCP client restarts the installed server within
seconds and the running exe locks its file against git.
