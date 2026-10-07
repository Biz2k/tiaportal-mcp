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
| `smoke.ps1` | `-Write`: calls the tools that change the project and undoes it; without: calls the read-only tools and checks that the project is not changed | yes |
| `finish.ps1` | Line endings, build, unit tests; with `-Install` updates `Install\TiaMcpServer` | no |
| `lad-instruction-probe.ps1` | Asks TIA Portal how it writes LAD instructions in a document: name, pins, what the compile wants | yes - a temporary block `MCPT_Ins`, deleted at the end |
| `inproc-call.ps1` | Calls tools of a built server inside PowerShell: no question about Openness access after a rebuild | yes - through the code of the server |
| `focus-probe.ps1` | Counts how often TIA Portal takes the keyboard focus during Openness calls | yes - the screen flickers; a temporary tag table |
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

Two runs of the built server against the running TIA Portal with the test project open. Both need a saved project: with
unsaved changes at the start they print that and exit with code 2; neither saves by itself. Exit code 1 when a call
fails. Run **both** after every change of the `Siemens\ layer, before `finish.ps1 -Install`.

- `smoke.ps1` (reading) - the 50 calls of `tools\smoke\read.json`: the project tree, the PLC `PLC (A0)`, the panel
  `HMI Unified/HMI_RT_3` and the PC station `АРМ Unified/HMI_RT_1`. After every call it reads the `isModified` flag of
  `get_project`: a read must not change the project, and the first call that does is named. Prints
  `smoke: N of N calls answered, M with an error; project modified by the run: no`. A new read tool gets a line in
  `read.json`.
- `smoke.ps1 -Write` - `tools\smoke\write.json` (about 210 calls, 6-7 minutes): a successful call of every tool that changes the
  project (the list is computed from the built server: the tools that `--read-only` leaves out; a tool without a call is
  named, so a new tool cannot stay unchecked), on objects named `MCPT_...`, with the undo of all of it. It makes a
  temporary PLC station with a second one and an ET 200SP (hardware, subnet, IO system, connection) and works on the
  PLC in it, never on the working PLCs; then WinCC Unified on the panel and on the PC station. What Openness may alter
  silently (a script, a formula, a list, a tag address) is read back and compared. Before and after, `inventory.json`
  (devices, topology, connections, PLC counts, screens, tags, lists, alarms, logs, scripts, runtime settings) is read and
  compared; a difference is printed. The project is **not saved**, so a failed undo does not reach the disk; afterwards it
  is marked modified, and with an identical inventory saving it is harmless. Objects of an earlier run that did not finish
  stop the next one (exit code 2). Not in the run: `download_to_plc` (PLCSIM, which the owner starts) and
  `save_as_project`, `close_project`, `open_project` - those are `tools\smoke\project.json`, run by hand on a copy of the project.
  Known limits of the platform it respects: a PC station refuses formulas and mappings of tag dynamizations; script
  modules cannot be deleted, so `MCPT_Mod` stays in both HMIs (empty).

`smoke/project.json` (not run by `smoke.ps1`; written in task 27, not yet run): `save_as_project` into a new folder, `save_project`,
`close_project`, `open_project` of the copy. Open a COPY of the test project in TIA Portal, then
`$f = 'C:/Temp/MCPT_SaveAs'; (Get-Content tools\smoke\project.json -Raw).Replace('{FOLDER}', $f).Replace('{PROJECT}', "$f/<name>.ap21") | Set-Content $env:TEMP\project-run.json -Encoding UTF8`
and `mcp-call.ps1 -Calls $env:TEMP\project-run.json`; it ends with no project open, and the copy stays on disk.

The calls file is UTF-8 JSON (station names are Cyrillic: write them as `\u` escapes). `{WORK}` in it is a temporary folder
that the run makes and removes. Besides `name` and `args`, a call may carry (see `mcp-call.ps1`): `expect` - text the
answer must contain (a failed check shows as `EXPECT-FAILED` in the header), `expectError` - the call must be refused,
`known` - a written-down defect: the failed check is reported apart and does not fail the run, and when it passes again
the run says to remove the mark. TIA Portal has to be running with the test project (`start-tia.ps1`).

## finish.ps1

Runs the unit tests that need no TIA Portal: the classes marked `[TestCategory("NoTia")]`
(`dotnet test --filter TestCategory=NoTia`). A new test class that needs no TIA Portal must carry
that category, or it is not run.

`-Install` stops the installed server (only processes under `Install\TiaMcpServer`) and copies
the build there. Commit right after it: the MCP client restarts the installed server within
seconds and the running exe locks its file against git.
