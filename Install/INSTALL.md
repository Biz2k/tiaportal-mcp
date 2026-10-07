# Installing and Integrating TiaMcpServer

This guide walks you through installing and integrating the precompiled `TiaMcpServer` into your preferred AI assistants (Claude Code, Claude Desktop, Codex, or Antigravity 2.0).

## 1. Installation

The `TiaMcpServer` folder contains everything needed to run the server.

1. Move the `TiaMcpServer` folder to a permanent location on your PC. A standard location is:
   `C:\Users\<YourUsername>\AppData\Local\TiaMcpServer\`
2. Remember this path, as you will need to provide the path to `TiaMcpServer.exe` in the integration steps below.

---

## 2. Integration with Claude Code (CLI)

[Claude Code](https://github.com/anthropics/claude-code) natively supports MCP servers via its configuration CLI.

Run the following command in your terminal, making sure to use the correct path to `TiaMcpServer.exe`:

```bash
claude mcp add tia-mcp-server -- C:\Users\<YourUsername>\AppData\Local\TiaMcpServer\TiaMcpServer.exe
```

This will automatically configure Claude Code to launch and communicate with `TiaMcpServer` via `stdio`. You can test it by running `claude` and asking it to `"Use the tia-mcp-server: run doctor and show the result"`.

---

## 3. Integration with Claude Desktop

To use `TiaMcpServer` directly in the Claude Desktop app:

1. Open the Claude Desktop configuration file located at:
   `%APPDATA%\Claude\claude_desktop_config.json`
2. Add the server to the `mcpServers` object, adjusting the path accordingly:

```json
{
  "mcpServers": {
    "tia-mcp-server": {
      "command": "C:\\Users\\<YourUsername>\\AppData\\Local\\TiaMcpServer\\TiaMcpServer.exe",
      "args": []
    }
  }
}
```
3. Fully quit and restart Claude Desktop.

---

## 4. Integration with Codex

[Codex](https://developers.openai.com/codex) (the desktop app and the CLI) keeps its MCP servers in one file,
`C:\Users\<YourUsername>\.codex\config.toml`. All three ways below write the same entry there.

### Option A: Settings of the desktop app

The names below are translated from the Russian interface of the app; yours may read slightly differently.

1. Open **Settings → Plugins**, press **Add** and choose the item that adds an MCP server.
2. In the form for a custom MCP server fill in:
   - **Name** - for example `tiamcpserver`;
   - **Type** - `STDIO` (not streamable HTTP);
   - **Launch command** - the full path to `TiaMcpServer.exe`;
   - **Arguments** - empty. Start-up options go in one per field: `--read-only` is one field,
     `--tia-major-version` and `20` are two; the button below the field adds another one;
   - **Environment variables** - not needed.
3. The server appears on the **MCP** tab in the **Servers** list with a switch. The switch turns
   it off without removing it - use it before updating the server files. The gear icon opens the
   same form for editing; the delete button is there too.

### Option B: Command line

```bash
codex mcp add tiamcpserver -- C:\Users\<YourUsername>\AppData\Local\TiaMcpServer\TiaMcpServer.exe
```

`codex mcp list` shows the server with the status `enabled`; `codex mcp remove tiamcpserver` removes it.

### Option C: Configuration file

Add to `C:\Users\<YourUsername>\.codex\config.toml`:

```toml
[mcp_servers.tiamcpserver]
enabled = true
command = 'C:\Users\<YourUsername>\AppData\Local\TiaMcpServer\TiaMcpServer.exe'
args = []
```

The path is in single quotes: TOML then takes the backslashes as they are. Start-up options go
into `args`, for example `args = ["--read-only"]`.

Start a new Codex session after any of the three, and test it by asking
`"Use tiamcpserver: run doctor and show the result"`.

---

## 5. Integration with Antigravity 2.0 (IDE)

Antigravity 2.0 configures MCP servers using an `mcp.json` file. You can configure it either **Globally** (for all your projects) or **Per-Workspace**.

### Option A: Global Configuration
1. Open or create the file `C:\Users\<YourUsername>\.gemini\config\mcp.json`.
2. Add the following JSON configuration, adjusting the path if you placed the folder elsewhere:

```json
{
  "servers": {
    "tia-mcp-server": {
      "type": "stdio",
      "command": "C:\\Users\\<YourUsername>\\AppData\\Local\\TiaMcpServer\\TiaMcpServer.exe",
      "args": []
    }
  }
}
```

### Option B: Workspace Configuration (VS Code / Antigravity)
1. In your project root, open or create `.vscode/mcp.json`.
2. Add the same JSON snippet as above.

Restart the Antigravity agent or IDE, and you will see the `tia-mcp-server` tools available in your context!

---

## Start-up options

Options go into `args` of the client configuration, for example `"args": ["--read-only"]` (in Codex: `args = ["--read-only"]`).

| Option | Purpose |
| --- | --- |
| `--tia-major-version <n>` | TIA Portal version. Default `21`. |
| `--read-only` | Do not register the tools that change the project. Without it, writing is available. |
| `--logging <1\|2\|3>` | `1` stderr, `2` debug output, `3` Windows event log. |
| `--doctor` | Print the environment report and exit. |
| `--debug-tools` | Register the server-development tools. |

TIA Portal must be running before you connect: the `connect` tool does not start it (unless called
with `startIfNotRunning=true`). PLCSIM control lives in a separate MCP server; for `download_to_plc`
the download target must already be running.

## Updating

The server files are locked while the MCP client runs. Close the client (or disable the server in
its settings) before replacing the contents of the `TiaMcpServer` folder. On the first start of a
new build TIA Portal asks for Openness access; confirm it.
