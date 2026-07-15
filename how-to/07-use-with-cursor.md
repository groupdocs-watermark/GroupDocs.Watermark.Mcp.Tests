# Use with Cursor

Connect the MCP server to [Cursor](https://cursor.com) so you can ask its Agent
to add, search, or remove document watermarks and inspect document structure.

## Prerequisites

- Cursor installed and updated (MCP support is in **Settings → Tools & MCP**).
- One of:
  - [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for the `dnx` route — recommended), or
  - [Docker](https://www.docker.com/products/docker-desktop) (for the container route).

## Config file location

Cursor uses the **`mcpServers`** key (like Claude Desktop) — **not** `servers`
as in VS Code. Two scopes:

| Scope | Path |
|---|---|
| Global (all projects) | `~/.cursor/mcp.json` (macOS/Linux) · `%USERPROFILE%\.cursor\mcp.json` (Windows) |
| Project-only | `.cursor/mcp.json` in the workspace root |

Create the file if it doesn't exist.

## Option A — dnx (recommended)

```json
{
  "mcpServers": {
    "groupdocs-watermark": {
      "command": "dnx",
      "args": ["GroupDocs.Watermark.Mcp@26.7.0", "--yes"],
      "env": {
        "GROUPDOCS_MCP_STORAGE_PATH": "/Users/you/Documents"
      }
    }
  }
}
```

- Replace the storage path with an **absolute path** to the folder Cursor should
  operate on. On Windows use `"C:\\Users\\you\\Documents"` (double-escaped) or
  forward slashes.
- Omit `@26.7.0` to always pull the latest stable.
- Add `"GROUPDOCS_LICENSE_PATH": "…/GroupDocs.Total.lic"` to `env` to run in
  licensed mode. All tools work in evaluation mode too, but `AddWatermark`,
  `AddImageWatermark`, and `RemoveWatermarks` produce output that carries an
  **additional evaluation watermark** alongside the one you requested; a license
  suppresses that extra watermark.

Copy-paste starter: [examples/cursor-mcp.json](../examples/cursor-mcp.json).

## Option B — Windows: full path to `dotnet.exe` (SSL / timeout workaround)

On Windows, Cursor launching `dnx` can fail with an **SSL / ~30 s timeout** on
the first package probe. Bypass `dnx` by running the already-cached tool DLL
directly with `dotnet.exe`:

```json
{
  "mcpServers": {
    "groupdocs-watermark": {
      "command": "C:\\Program Files\\dotnet\\dotnet.exe",
      "args": [
        "C:\\Users\\you\\.nuget\\packages\\groupdocs.watermark.mcp\\26.7.0\\tools\\net10.0\\any\\GroupDocs.Watermark.Mcp.dll"
      ],
      "env": {
        "GROUPDOCS_MCP_STORAGE_PATH": "C:\\Users\\you\\Documents"
      }
    }
  }
}
```

Populate the cache first by running `dnx GroupDocs.Watermark.Mcp@26.7.0 --yes` once
in a terminal, then point `args[0]` at the resulting
`…\.nuget\packages\groupdocs.watermark.mcp\<version>\tools\net10.0\any\GroupDocs.Watermark.Mcp.dll`.

## Option C — Docker

```json
{
  "mcpServers": {
    "groupdocs-watermark": {
      "command": "docker",
      "args": [
        "run", "--rm", "-i",
        "-v", "/Users/you/Documents:/data",
        "ghcr.io/groupdocs-watermark/watermark-net-mcp:26.7.0"
      ]
    }
  }
}
```

The Docker image bundles `libgdiplus`, `libfontconfig1`, and the MS core fonts,
so text-watermark glyph rendering works with zero host setup.

## Reload and verify

1. Save `mcp.json`.
2. **Settings → Tools & MCP** → find `groupdocs-watermark` → toggle it on (or hit
   the reload icon). A green dot means it connected.
3. Expand it — you should see `add_watermark`, `add_image_watermark`,
   `search_watermarks`, `remove_watermarks`, and `get_document_info`.

## Example prompts (Agent mode)

```
Add a "DRAFT" watermark to report.pdf.

Stamp company-logo.png as an image watermark on contract.docx at 30% opacity.

What watermarks does invoice.pdf contain?

Remove all watermarks from final-draft.docx so I can ship it.

How many pages does presentation.pptx have, and what are their dimensions?
```

The Agent will call `add_watermark` / `add_image_watermark` / `search_watermarks`
/ `remove_watermarks` / `get_document_info` and compose its answer from the
results.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Server greyed out / won't start on Windows | `dnx` SSL/timeout — use **Option B** (full `dotnet.exe` path + cached DLL). |
| Server not listed | JSON typo — Cursor silently drops unparseable entries. Validate with `jq . mcp.json`. Confirm the key is `mcpServers`, not `servers`. |
| Output has an extra "Evaluation" watermark | Expected in evaluation mode. Add `GROUPDOCS_LICENSE_PATH` to run licensed and suppress the extra watermark. |
| `DllNotFoundException: libgdiplus` (macOS/Linux) | Install native deps — `brew install mono-libgdiplus` (macOS) / `apt-get install libgdiplus libfontconfig1 ttf-mscorefonts-installer` (Linux), or use the Docker option. |

## Next steps

- [04 — Use with Claude Desktop](04-use-with-claude-desktop.md)
- [05 — Use with VS Code / Copilot](05-use-with-vscode-copilot.md)
