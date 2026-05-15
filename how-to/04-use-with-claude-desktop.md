# Use with Claude Desktop

Connect the MCP server to Claude Desktop (macOS / Windows) so you can ask
Claude to add text watermarks to your documents or inspect existing watermarks.

## Prerequisites

- [Claude Desktop](https://claude.ai/download) installed and logged in.
- One of:
  - [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for the `dnx` route — recommended), or
  - [Docker](https://www.docker.com/products/docker-desktop) (for the container route).

## Config file location

| OS | Path |
|---|---|
| macOS | `~/Library/Application Support/Claude/claude_desktop_config.json` |
| Windows | `%APPDATA%\Claude\claude_desktop_config.json` |

Create the file if it doesn't exist.

## Option A — dnx (recommended)

```json
{
  "mcpServers": {
    "groupdocs-watermark": {
      "type": "stdio",
      "command": "dnx",
      "args": ["GroupDocs.Watermark.Mcp@26.4.4", "--yes"],
      "env": {
        "GROUPDOCS_MCP_STORAGE_PATH": "/Users/you/Documents"
      }
    }
  }
}
```

- Replace `/Users/you/Documents` with an **absolute path** to the folder
  containing documents you want Claude to operate on.
- On Windows use `"C:\\Users\\you\\Documents"` (double-escaped backslashes) or
  forward slashes: `"C:/Users/you/Documents"`.

Full example: [examples/claude-desktop.json](../examples/claude-desktop.json).

### If Claude can't find `dnx`

Claude Desktop launches child processes with a minimal PATH — `dnx` may not be
found on macOS even though it works in your shell. Use the absolute path:

```json
"command": "/usr/local/share/dotnet/dnx"
```

On Windows:

```json
"command": "C:\\Program Files\\dotnet\\dnx.cmd"
```

Find the correct path with:

```bash
which dnx            # macOS / Linux
where dnx.cmd        # Windows (from cmd)
```

## Option B — Docker

```json
{
  "mcpServers": {
    "groupdocs-watermark": {
      "type": "stdio",
      "command": "docker",
      "args": [
        "run", "--rm", "-i",
        "-v", "/Users/you/Documents:/data",
        "ghcr.io/groupdocs-watermark/watermark-net-mcp:26.4.4"
      ]
    }
  }
}
```

This works even if you don't have the .NET SDK installed. The first invocation
pulls the image; subsequent launches are fast.

## Option C — Global dotnet tool

```json
{
  "mcpServers": {
    "groupdocs-watermark": {
      "type": "stdio",
      "command": "groupdocs-watermark-mcp",
      "env": {
        "GROUPDOCS_MCP_STORAGE_PATH": "/Users/you/Documents"
      }
    }
  }
}
```

Requires you've already run `dotnet tool install -g GroupDocs.Watermark.Mcp`
(see [01 — NuGet install](01-install-from-nuget.md)).

## Restart Claude Desktop

After editing the config, fully quit and reopen Claude Desktop. On macOS,
`Cmd+Q` — closing the window isn't enough.

## Verify the connection

1. Open a new conversation.
2. Click the **🔨 tools** icon in the composer — you should see
   `search_watermarks` and `add_watermark` listed under `groupdocs-watermark`.
3. If the icon shows an error badge, hover for the details. The most common
   issue is a bad `command` path or invalid `GROUPDOCS_MCP_STORAGE_PATH`.

## Example prompts

```
Add a 'DRAFT' watermark to report.pdf.

Stamp 'CONFIDENTIAL' as a watermark on contract.docx with a 24-point font and -30 degree rotation.

What watermarks does invoice.pdf contain?

Search slides.pptx for watermarks and tell me which pages have them.

List the watermarks in original.docx and final.docx — is anything different?
```

Claude will call `add_watermark` (and `search_watermarks` where relevant) and
compose its answer from the tool results.

## License note

`add_watermark` and `search_watermarks` work in evaluation mode without a
GroupDocs license — the engine simply adds an additional evaluation watermark
to the output alongside the user-requested one. To suppress the evaluation
watermark, add the license path to your config:

```json
"env": {
  "GROUPDOCS_MCP_STORAGE_PATH": "/Users/you/Documents",
  "GROUPDOCS_LICENSE_PATH": "/Users/you/.secrets/GroupDocs.Total.lic"
}
```

## Troubleshooting

| Symptom | Fix |
|---|---|
| Server not listed in tools icon | Config JSON has a typo — Claude silently drops unparseable entries. Run it through `jq . claude_desktop_config.json`. |
| Server listed but greyed out | Claude couldn't launch the process. Check `~/Library/Logs/Claude/mcp*.log` on macOS or `%APPDATA%\Claude\logs\mcp*.log` on Windows for stderr from the server. |
| "[Evaluation mode] Output may include evaluation watermarks…" banner in tool output | Expected in evaluation mode. Both tools still work — set `GROUPDOCS_LICENSE_PATH` to suppress the banner and the extra eval watermark. |
| `Watermarking failed for '<file>': System.DllNotFoundException` (Linux only) | Missing `libgdiplus` / MS core fonts | Use the published Docker image or install `libgdiplus libfontconfig1 ttf-mscorefonts-installer`. |

## Next steps

- [05 — Use with VS Code / Copilot](05-use-with-vscode-copilot.md)
- [03 — MCP registry](03-verify-mcp-registry.md) — confirm the snippet matches what's on nuget.org
