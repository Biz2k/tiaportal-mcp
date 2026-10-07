# TiaMcpServer.Test

MSTest project verifying portal connectivity, project handling, devices, and MCP server behavior.

## Environment Prerequisites

- .NET Framework 4.8 installed
- Siemens TIA Portal V21 installed and running
- User in Windows group "Siemens TIA Openness"
- Env var `TiaPortalLocation` set to `C:\\Program Files\\Siemens\\Automation\\Portal V21`

## Test Assets
- `assets/TestProject1.zap20` – archived local project used in tests. Retrieve it with TIA Portal V21 (which upgrades it to `.ap21`) and point `Settings.cs` at the retrieved project.
- A multi-user local session (`.als21`) – create this manually for session tests and point `Settings.cs` at it.

See `Settings.cs` for configuration options such as project paths and timeouts.

## Test Categories

- `[TestCategory("NoTia")]` - needs no TIA Portal (the pure logic of the tools): `dotnet test --filter TestCategory=NoTia`.
  `tools\finish.ps1` runs exactly these. Every new class of this kind must carry the category.
- `[TestCategory("NeedsTia")]` - `Test1Portal` .. `Test6Diagnostics`, `Test21Project`, `Test22Session`. They were written
  for the original author's setup (TIA Portal and projects at the paths in `Settings.cs`), are not maintained and do not
  know the newer tools. They carry `[Ignore]`, so `dotnet test` without a filter skips them instead of failing on paths
  that exist only on another machine. No test in them turned out to be pure logic; those that need no TIA Portal live in
  the `NoTia` classes.

## Live checks

`tools/smoke.ps1` calls the read-only tools on the owner's test project through the built server and counts the errors (`-Grouped`: through the group tools, as a client calls them);
`tools/mcp-call.ps1` runs any calls file. They replace the old live tests (see `tools/README.md`).

To run an old class anyway: remove its `[Ignore]` line, point `Settings.cs` at your projects (retrieve
`assets/TestProject1.zap20` with TIA Portal V21, create a multi-user session for the session tests) and run
`dotnet test --filter TestCategory=NeedsTia` with TIA Portal open. They may change the projects they open.

## Test Execution Policy

- Offer to run tests, but only execute them after explicit user confirmation. See root `AGENTS.md` for details.
