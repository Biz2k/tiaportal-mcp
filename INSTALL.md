# Installing and Integrating TiaMcpServer

This guide walks you through building, installing, and integrating `TiaMcpServer` into your preferred AI assistants (Claude Code, Antigravity 2.0, or Claude Desktop).

## 1. Installation

A PowerShell script is provided to compile and install the application automatically.

1. Open PowerShell and navigate to the root directory of this repository.
2. Run the installer script:
   ```powershell
   .\Install.ps1
   ```
3. The script will build the project using `.NET` in `Release` mode and copy the standalone executables and necessary DLLs to your Local AppData folder:
   `C:\Users\<YourUsername>\AppData\Local\TiaMcpServer\`

*Note: You can specify a custom installation directory by running `.\Install.ps1 -InstallDir "C:\Custom\Path"`.*

---

## 2. Integration with Claude Code (CLI)

[Claude Code](https://github.com/anthropics/claude-code) natively supports MCP servers via its configuration CLI.

Run the following command in your terminal, replacing `<YourUsername>` with your actual Windows username:

```bash
claude mcp add tia-mcp-server -- C:\Users\<YourUsername>\AppData\Local\TiaMcpServer\TiaMcpServer.exe
```

This will automatically configure Claude Code to launch and communicate with `TiaMcpServer` via `stdio`. You can test it by running `claude` and asking it to check the connection to the PLCSIM instance (`"Use the tia-mcp-server to list plcsim instances"`).

---

## 3. Integration with Antigravity 2.0 (IDE)

Antigravity 2.0 configures MCP servers using an `mcp.json` file. You can configure it either **Globally** (for all your projects) or **Per-Workspace**.

### Option A: Global Configuration
1. Open or create the file `C:\Users\<YourUsername>\.gemini\config\mcp.json`.
2. Add the following JSON configuration:

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

## 4. Integration with Claude Desktop

To use `TiaMcpServer` directly in the Claude Desktop app:

1. Open the Claude Desktop configuration file located at:
   `%APPDATA%\Claude\claude_desktop_config.json`
2. Add the server to the `mcpServers` object:

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
