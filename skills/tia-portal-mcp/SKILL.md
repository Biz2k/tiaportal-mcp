---
name: tia-portal-mcp
description: How to work on a Siemens TIA Portal project through the tia-mcp-server MCP server without losing the user's work - connect, find objects, read and change PLC software (blocks, SCL code, data types, tags), hardware and PROFINET networks, WinCC Unified screens, tags and alarms, compile, save, and download to a PLC or PLCSIM. Use this skill whenever the tia-mcp-server tools (open_tia_project, plc_*, hw_*, net_*, unified_*, download_to_plc) are available and the request touches a TIA Portal project, a Siemens PLC program or an HMI - also for read-only questions about the project, and even when the user does not name the server.
---

# Working with TIA Portal through tia-mcp-server

The server drives the TIA Portal the user has open, through the Openness API. It is their live
engineering session: a wrong call can change a real project, and a few calls can close TIA Portal
with everything unsaved. This skill is the order of work and the rules; the parameters of each
tool are in the tool descriptions and in `docs/tools/` of the server repository
(<https://github.com/Biz2k/tiaportal-mcp>).

If a tool named here does not exist, the server has moved on: trust the tool list of the session,
and look in `CHANGELOG.md` of the repository for the new name.

## 1. Connect and make sure it is the right project

1. TIA Portal must already be running. The server attaches to it and never starts it. If it is
   not running, ask the user to start it.
2. `get_state` tells whether the server is connected, which project is open and whether writing
   is allowed (`allowWrite`). When not connected, its answer says what to call next.
3. Open with `open_tia_project` (absolute path of the project or session file), or `connect` when
   the project is already open in TIA Portal. With several TIA Portal instances,
   `get_tia_instances` lists them.
4. Before the first change, read `get_project` and compare the project name with what the user
   asked for. The user may have another project open than the one you expect.
5. Before a longer piece of work, warn the user: while you write or compile, TIA Portal takes the
   keyboard focus at moments nobody chooses, and a key pressed then may cancel your operation.
6. If connecting answers that TIA Portal is waiting for the user to grant Openness access, ask the
   user to open TIA Portal and confirm it there, wait for their reply, then call `connect` again.

## 2. Find objects, do not guess paths

A guessed path costs a failed call at best; at worst it matches a different object with a similar
name, and the change lands there.

- Paths are relative to the root of their tree: `1_Tests/FC_Block_1`, not
  `Program blocks/1_Tests/FC_Block_1`.
- Discover them: `get_project_tree` for the project, `plc_get_software_tree` for one PLC,
  `hw_get_devices` for stations. `plc_resolve_object_path` turns a bare name into a path.
- A `/` inside a name is written `%2F`. Listings already return paths in that form - copy them.
- Large projects produce large answers. Ask for the part you need (`sections` of
  `plc_get_software_tree`, a group path, the paging arguments) instead of the whole tree.

## 3. Change the project

- **Every write is in memory until `save_project`.** Nothing is lost by a wrong write that was
  not saved; everything is lost if TIA Portal closes before the save.
- **Saving is the user's decision.** Save when the user asked for the change to be kept or agreed
  to it. Otherwise finish by saying what was changed and that it is not saved yet.
- A write runs in a transaction: it either happens completely or not at all. The `*_manage_*`
  tools take a list of actions and apply the list all or nothing - put related changes in one call.
- **Read back after writing.** Use the matching `get` tool to confirm the result instead of
  trusting the write answer alone: TIA Portal can accept a value and store it differently.
- **Compile after changing code or types** (`plc_compile_block`, `plc_compile_software`,
  `unified_compile`) and read the messages. An object that is not consistent cannot be exported,
  copied or moved, and the project is not ready to download.
- **Before deleting or renaming**, look at who uses the object (`plc_where_used`,
  `plc_get_cross_references`) and tell the user what depends on it.
- A write shows its tool and step in a TIA Portal window; Cancel there stops it with nothing
  changed. Do not repeat a cancelled call without asking.
- The tools named here are called through group tools (plc_read, plc_write, ...) as
  `{"tool": "<name>", "arguments": {...}}`; 'tia_help' gives the parameters. `--full` shows them singly.
- If the write tools are missing from the session, the server runs with `--read-only`
  (`get_state` shows `allowWrite: false`). Say so; do not look for a way around it.
- **Protection, passwords, users (`sec_*`)**: before each call say what will change on which PLC,
  user, role or block and wait for the user's yes. Use only a password the user gave; never invent,
  guess or repeat one. A know-how protected block or a protected project is opened only with what the user gave.
  `sec_protect_project` cannot be undone: call it only on an explicit request, after a clear yes.

## 4. When a call fails

- The error text carries the reason from TIA Portal, a code and the paths of the call. Act on the
  text: `NotFound` names the tool that lists valid paths, `InvalidState` says what to do first
  (usually compile).
- `NotSupported` means Openness has no such operation. No other input will make it work - stop
  retrying and tell the user.
- **"TIA Portal was closed by the call..." or "disposed object"**: TIA Portal is gone or the
  project was closed. Stop. Tell the user which call it was and that unsaved changes are lost.
  After they start TIA Portal again, `connect`. Do not repeat the call that closed it.
- A call that hangs right after the server was updated usually waits for the Openness access
  prompt inside TIA Portal. Only the user can confirm it.

## 5. Download to a PLC

A download changes a running controller. Do it only when the user asked for this download.

1. The user starts the PLC or the PLCSIM instance and sets its address to match the project.
   Never start PLCSIM yourself.
2. `get_download_targets` lists the targets; pick the one the user confirmed, not the first one.
3. `download_to_plc`. There is no preview: the call loads. The CPU is not stopped or started
   unless asked through the arguments, and a download that needs a stop is refused with that
   explanation. A hardware download normally needs the stop - say so before the call and ask
   the user before stopping a CPU.
4. Report every step and message from the answer, including steps that kept TIA Portal's preset.

## 6. Know the limits before promising

- HMI tools work on **WinCC Unified** only. Comfort, Advanced and Professional panels are refused.
- Blocks are created from SCL source, from a minimal template (LAD, FBD, STL) or by import.
  LAD blocks are written as text: `plc_get_lad_instructions` first (names cannot be guessed), then
  `plc_create_lad_block` or `plc_manage_lad_networks`. FBD, STL, GRAPH and CEM code is read only.
- Safety (F) blocks and know-how protected objects reject most edits.
- Block names, block numbers and data type names are unique within a PLC: a copy inside one PLC
  needs a new name.

The full list is under "Known limitations" in `README.md` of the repository.

## 7. Finish with a report

The user cannot see what happened inside TIA Portal from the chat, so end every task that
changed something with these four lines:

- **Changed:** the objects created, changed or deleted, with their paths.
- **Compiled:** yes with the error and warning counts, or no.
- **Saved:** yes, or "not saved - the changes are in memory only".
- **Open points:** what failed, what was skipped, what the user has to do in TIA Portal.

## 8. Where the details are

| Need | Place in the repository |
|---|---|
| Parameters of a tool | `docs/tools/` (generated from the tool descriptions) |
| A scenario of several tools, run on a real project | `docs/recipes/` |
| Paths, hardware order, download, limitations | `README.md` |
| Renamed and removed tools | `CHANGELOG.md` |
