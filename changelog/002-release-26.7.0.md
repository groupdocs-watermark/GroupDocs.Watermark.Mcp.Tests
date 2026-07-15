---
id: 002
date: 2026-07-15
version: 26.7.0
type: maintenance
---

# Target GroupDocs.Watermark.Mcp 26.7.0

## What changed
- Bumped the package-under-test default `McpPackageVersion` `26.5.0` → `26.7.0`
  (`Directory.Build.props`) and the `integration.yml` workflow defaults
  (`workflow_dispatch` input + `MCP_PACKAGE_VERSION` fallback).
- Bumped every `@26.5.0` doc pin to `@26.7.0` across `how-to/*`, `examples/*`,
  `README.md`, `AGENTS.md`, `llms.txt`, and `docker-scripts/*`.
- **Added a Cursor deployment guide**: `how-to/07-use-with-cursor.md` +
  `examples/cursor-mcp.json`. Covers the `dnx` route, the Windows
  `dotnet.exe` + cached-DLL SSL/timeout workaround, and the Docker route, with
  Watermark-specific example prompts and the eval-mode watermark note. Linked
  from `how-to/README.md` and `llms.txt`.

## Why
Keeps the integration suite pinned to the newly-released server (engine bumped to
GroupDocs.Watermark 26.6.0) and adds Cursor to the documented client matrix,
matching the cross-product convention established in the Metadata Tests repo.

## Migration / impact
Test surface unchanged — still 5 tools (`ToolDiscoveryTests` asserts 5). The
integration tests only exercise the published NuGet, so they go green once
`GroupDocs.Watermark.Mcp@26.7.0` is live on nuget.org.
