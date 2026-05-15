# How-to guides

Step-by-step guides for verifying and using every deployment channel of
[`GroupDocs.Watermark.Mcp`](https://www.nuget.org/packages/GroupDocs.Watermark.Mcp).

Each guide is self-contained — pick the one that matches your workflow. They
all point at the same published artifact (`26.4.4` at time of writing).

| # | Guide | When to use |
|---|---|---|
| 01 | [Install from NuGet (dnx + dotnet tool)](01-install-from-nuget.md) | You have the .NET 10 SDK. Fastest path — no Docker required. |
| 02 | [Run via Docker](02-run-via-docker.md) | You'd rather not install .NET, or want isolation from the host. |
| 03 | [Verify on the MCP registry](03-verify-mcp-registry.md) | You want to confirm the package shows up in MCP clients' discovery UIs and that its `server.json` metadata is correct. |
| 04 | [Use with Claude Desktop](04-use-with-claude-desktop.md) | Connect from Claude Desktop (macOS / Windows). |
| 05 | [Use with VS Code / GitHub Copilot](05-use-with-vscode-copilot.md) | Connect from VS Code's MCP support or GitHub Copilot agents. |
| 06 | [Run the integration tests](06-run-integration-tests.md) | Validate a specific published version end-to-end; set up CI. |

## Which guide first?

- **Trying the server for the first time** → start with
  [01 — NuGet via dnx](01-install-from-nuget.md). One command, no install.
- **Debugging a broken release** → [06 — Integration tests](06-run-integration-tests.md),
  then cross-check with [03 — MCP registry](03-verify-mcp-registry.md).
- **Wiring an AI agent to production documents** → pick your client:
  [04 — Claude Desktop](04-use-with-claude-desktop.md) or
  [05 — VS Code](05-use-with-vscode-copilot.md).

## Common context

- All guides target `GroupDocs.Watermark.Mcp@26.5.0`. Substitute a newer version
  freely — the interfaces haven't changed.
- Tools exposed on the wire are `add_watermark` and `search_watermarks` (snake_case).
- Both tools work in evaluation mode without a GroupDocs license. The engine
  simply adds an additional evaluation watermark alongside the user-requested
  one. The response prefix is `"[Evaluation mode] Output may include evaluation
  watermarks alongside the user-requested watermark."` See each guide's "License"
  section for how to point the server at a `.lic` to suppress this.
