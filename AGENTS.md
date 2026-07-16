# AGENTS.md — Guide for AI coding agents

Brief orientation for AI coding agents (Claude Code, Copilot, Cursor, Aider, Amp, Codex) working in this repository.

## What this repo is

**Integration tests** for the [`GroupDocs.Watermark.Mcp`](https://www.nuget.org/packages/GroupDocs.Watermark.Mcp) NuGet package — an MCP server that exposes GroupDocs.Watermark for .NET as AI-callable tools.

This repo is **not** the server itself. The server lives at [groupdocs-watermark/GroupDocs.Watermark.Mcp](https://github.com/groupdocs-watermark/GroupDocs.Watermark.Mcp). This repo:

1. Consumes only the **published** NuGet artifact (no project references).
2. Launches the server via `dnx`, connects as an MCP stdio client, and exercises every advertised tool.
3. Doubles as a copy-pasteable set of example configs and how-to guides for all deployment channels (NuGet, Docker, MCP registry, Claude Desktop, VS Code).

## Folder layout

```
src/GroupDocs.Watermark.Mcp.Tests/
  Fixtures/
    McpServerFixture.cs          ← launches dnx child process, wires stdio MCP client
    SampleDocuments.cs           ← builds minimal PDF + JPEG from byte arrays at runtime
    ToolCatalog.cs               ← keyword-based tool name resolution (add/search)
    ToolResponse.cs              ← CallToolResult text/JSON extraction
    CommandResolver.cs           ← cross-platform dnx.cmd resolution on Windows
    PackageVersion.cs            ← pulls version from env / assembly metadata / default
  McpServerTestBase.cs           ← per-test fixture base — fresh server process per test method
  ToolDiscoveryTests.cs          ← handshake, tools/list, schema validation
  AddWatermarkTests.cs           ← text watermark across formats + custom font/rotation + password
  AddImageWatermarkTests.cs      ← image watermark across formats
  SearchWatermarksTests.cs       ← zero-watermark + real-sample JSON shape + add-then-search roundtrip + password
  RemoveWatermarksTests.cs       ← strip-all + text-filter + roundtrip with search
  GetDocumentInfoTests.cs        ← file type + page count + per-page dimensions
  ErrorHandlingTests.cs          ← unknown file, corrupted bytes, password parameter
  GroupDocs.Watermark.Mcp.Tests.csproj
.github/workflows/integration.yml  ← matrix × 3 OS, nightly cron, release-smoke dispatch
changelog/                         ← one MD file per change (NNN-slug.md)
how-to/                            ← user-facing guides for every deployment channel
examples/                          ← claude-desktop.json, vscode-mcp.json, docker-compose.yml
sample-docs/                       ← drop real fixture files here; copied to test output
Directory.Build.props              ← McpPackageVersion property (overridable)
global.json                        ← pinned to .NET 10.0.100
```

## What gets tested

| Area | Covered by |
|---|---|
| Package installs and starts via `dnx` | `McpServerFixture` |
| MCP handshake, server info, tool list (5 tools) | `ToolDiscoveryTests` |
| `add_watermark` — synthetic PDF + 6 real samples + custom font/rotation + password | `AddWatermarkTests` |
| `add_image_watermark` — image-on-document overlay across formats | `AddImageWatermarkTests` |
| `search_watermarks` — zero-watermark + real-sample JSON shape + add-then-search roundtrip + password | `SearchWatermarksTests` |
| `remove_watermarks` — strip-all + text-filter + roundtrip with search | `RemoveWatermarksTests` |
| `get_document_info` — file type, page count, per-page dimensions | `GetDocumentInfoTests` |
| Unknown / corrupted files, password parameter | `ErrorHandlingTests` |

## Commands you can run

```bash
# Restore + build
dotnet restore
dotnet build -c Release

# Run all integration tests against the default package version (26.7.0)
dotnet test -c Release

# Run against a specific published version
dotnet test -c Release -p:McpPackageVersion=26.7.0
# or
MCP_PACKAGE_VERSION=26.7.0 dotnet test -c Release

# Pass through GROUPDOCS_LICENSE_PATH to suppress the evaluation watermark added
# alongside the user-requested one. Tests pass with or without; license only
# affects banner-prefix assertions.
GROUPDOCS_LICENSE_PATH=/path/to/GroupDocs.Total.lic dotnet test -c Release

# Run just the discovery suite (fastest — no tool invocations)
dotnet test -c Release --filter "FullyQualifiedName~ToolDiscovery"
```

## Key design decisions

1. **Keyword-based tool resolution.** `ToolCatalog.Resolve("add")` picks the tool whose name contains "add" (case-insensitive). The MCP C# SDK converts `[McpServerTool]` method names to `snake_case` — so the actual wire names are `add_watermark` and `search_watermarks`, not `AddWatermark`. Tests stay robust if that convention changes. (Pitfall observed in the broader product family: keywords with multi-word PascalCase tools like `GetDocumentInfo` must use the underscored form `document_info`, not `documentinfo` — substring match against `get_document_info` requires the underscore.)

2. **Synthetic fixtures.** `SampleDocuments.cs` builds a minimal valid PDF (1 page, Info dict → Author/Title) and a valid baseline JPEG from byte arrays. No binary files in the repo. To add real-world fixtures, drop them in `sample-docs/` — the csproj auto-copies everything there to the test output.

3. **Per-test server process — required by the eval-mode document cap.** Every test class derives from `McpServerTestBase`, which boots a **fresh MCP server process for each test method** (xUnit instantiates the test class per method; the base's `IAsyncLifetime` hooks start/stop a `McpServerFixture` each time). This is NOT the usual shared-`ICollectionFixture` pattern, and it must not be "optimized" back to one: GroupDocs.Watermark's evaluation mode caps document loads at **10 per process** (`"Only 10 documents can be loaded per application run in evaluation mode"`). The full suite opens ~30 documents — a single shared server process exhausts the budget partway through and the rest of the suite fails. A fresh process per test resets the counter; no individual test opens more than ~3 documents. See Pitfall #20 in the clone-to-new-product.md prompt.

4. **Evaluation-mode is non-blocking (for watermark stamping).** Unlike `GroupDocs.Metadata.Save()` which throws in evaluation mode, `GroupDocs.Watermark` adds an additional evaluation watermark alongside the user-requested one and saves the output. With the per-test fixture (decision 3) the suite runs fully **unlicensed** — no `GROUPDOCS_LICENSE` secret is needed for green CI. The license only affects the eval-mode banner prefix in `AddWatermark`'s response text; the optional `GROUPDOCS_LICENSE`-decode step in `integration.yml` exists solely to verify the no-banner case when a license *is* available.

5. **JSON responses are returned raw.** `SearchWatermarks` calls `JsonSerializer.Serialize(...)` directly without piping through `OutputHelper.TruncateText` — the truncation marker is plain text and would break strict-JSON consumers. Test fixtures parse responses with `JsonDocument.Parse` and assert against the `{ count, watermarks: [...] }` schema.

6. **Engine errors surface diagnostically.** Both `AddWatermarkTool` and `SearchWatermarksTool` wrap their engine calls in `try/catch` and return `"Watermarking failed for '<file>': <ExceptionType>: <message> | inner(0): ..."` (or `"Search failed for '<file>': ..."`) instead of letting them bubble up to MCP's canned `"An error occurred invoking '<tool>'"` wrapper. Tests assert `DoesNotContain("Watermarking failed for", body)` on the success path.

7. **No project references to the server.** The csproj only references `ModelContextProtocol` 1.1.0. If the server source breaks in the sibling repo, these tests still pass — they validate the shipped NuGet artifact.

## House rules

1. **Changelog entries required** — any PR that changes behaviour adds `changelog/NNN-slug.md` (schema in `changelog/README.md`).
2. **How-to guides track deployment reality** — if the main repo publishes a new channel (e.g. new Docker registry), add a guide under `how-to/` *and* update `README.md`.
3. **Version bumps flow through `Directory.Build.props`** — `<McpPackageVersion>` is the single source of truth for "what version are we testing." CI overrides it via env var / workflow input.
4. **Tests must not require the main repo's source.** If a test needs a server-side change, file an issue there — don't work around it here.
5. **Target framework is `net10.0` only** — required by `dnx` and the MCP SDK.

## Release smoke hook

The main repo's `publish_prod.yml` should fire a `repository_dispatch` with `event_type=nuget-published` after `dotnet nuget push` succeeds. The workflow in `.github/workflows/integration.yml` consumes `client_payload.package_version` and runs the matrix against the just-published version. This closes the loop: publish → smoke-test live nuget.org → fail loud if broken.

## What NOT to change

- Do not add a `ProjectReference` to the main repo's `GroupDocs.Watermark.Mcp.csproj`. This repo exists to test the shipped NuGet, not the source.
- Do not hardcode tool names as string literals (`"search_watermarks"`). Use `ToolCatalog.AddWatermark.Name` / `ToolCatalog.SearchWatermarks.Name`.
- Do not commit real license files or binary fixtures with unclear provenance. License goes through the `GROUPDOCS_LICENSE` CI secret; fixtures in `sample-docs/` must be self-authored or CC0/Apache-2.0.
