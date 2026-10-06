# Agents Guide

This repository can be used with agentic coding assistants. Follow these guidelines to collaborate safely and efficiently.

## Start Here

Development of this server is continued from a handoff package. Before any work, read
[`docs/handoff/README.md`](docs/handoff/README.md): it leads to the project context, the rules
that protect the user's TIA Portal session, the open tasks, and the scripts in `tools/`.

The test policy below is the general rule for unknown environments. For the owner's own test
project the standing permissions are written down in `docs/handoff/context.md`.
## Test Execution Policy

- Offer to run tests, but only run them after explicit user confirmation.
- Tests may require user‑specific environment conditions (e.g., installed TIA Portal, licenses, PLC project assets), so do not assume they will pass in your environment.
- When offering to run tests, clearly state prerequisites and potential side effects.
- If the user declines or does not respond, provide concise instructions for the user to run tests locally instead of running them yourself.

### Standard Commands

```powershell
dotnet test
```

If tests need to write to temporary locations or access external resources, note these requirements up front.

## How To Ask For Confirmation

Use clear, actionable language. For example:

- "I can run `dotnet test` to validate the changes. Some tests require TIA Portal and project assets on this machine. Do you want me to run them now?"
- If approved: proceed and summarize results. If not approved: provide steps the user can run.

## Environment Considerations

- Respect the user's environment constraints (e.g., offline, restricted permissions, licensed software).
- If a command fails due to environment limitations, do not retry destructively; report the exact failure and suggest alternatives.
- Document any meaningful limitations or deviations in the commit message or relevant README, per the Contributor Guidelines.


## Formatting & Encoding

- Preserve existing indentation style (tabs vs. spaces).
- Do not modify file encodings; keep UTF-8 BOM where present.
- Ensure Windows CRLF line endings are retained when editing files.

## PLCSIM Execution Policy

- Do not attempt to autonomously launch and power on PLCSIM or PLCSIM Advanced via background terminal commands or headless API during the download process.
- Background execution lacks UI visibility and may fail due to unconfigured IP settings in headless instances.
- Always instruct the user to:
  1. Manually launch PLCSIM (or PLCSIM Advanced).
  2. Create a virtual controller instance with the correct IP address (matching the TIA Portal project).
  3. Power on the instance.
  ...before initiating a download task via Openness.

## Skill For Users Of The Server

[`skills/tia-portal-mcp/SKILL.md`](skills/tia-portal-mcp/SKILL.md) teaches an agent how to work on a TIA Portal project
with this server: the order of work, the rules that protect the user's project, what to do when a call fails. It is
kept thin on purpose and grows with the server.

Update it in the same commit as the change when:

- a tool it names is renamed or removed. Check with
  `grep -oE '`[a-z]+(_[a-z]+)+`' skills/tia-portal-mcp/SKILL.md | tr -d '`' | sort -u | grep -vxFf docs/tools-list.txt` -
  it prints the names the server no longer has, and nothing when all is well;
- the way to connect, save or download changes, or the write policy does;
- a call turns out to close TIA Portal or lose work, and a user of the server can reach it through a tool;
- a limitation that makes a common request impossible appears or is lifted.

Do not put into it:

- parameters or lists of tools - `docs/tools/` is generated from the code;
- step-by-step scenarios - they go to `docs/recipes/`, the skill links there;
- notes for developing the server - they go to `docs/handoff/`.

If an agent needs the skill to call a tool correctly, fix the description of the tool, not the skill. Keep the skill
under about 120 lines. Before a release, read it once against `README.md`.
