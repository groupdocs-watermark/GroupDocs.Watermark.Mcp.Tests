---
id: 001
date: 2026-05-14
version: 25.6.0
type: feature
---

# Initial integration-test repo for GroupDocs.Watermark.Mcp 25.6.0

## What changed
- Public integration-test repo `groupdocs-watermark/GroupDocs.Watermark.Mcp.Tests` published.
- Exercises the **shipped** `GroupDocs.Watermark.Mcp@25.6.0` NuGet via `dnx`, NOT a project reference to the server source.
- Test suites (covering all 5 advertised tools):
  - `ToolDiscoveryTests` — server handshake, tool listing (asserts exactly 5 tools: `add_watermark`, `add_image_watermark`, `search_watermarks`, `remove_watermarks`, `get_document_info`), schema sanity.
  - `AddWatermarkTests` — text-watermark insertion against the synthetic 1-page PDF and 6 real samples (PDF / DOCX / XLSX / PPTX / PNG / VSDX), plus custom font/rotation arguments and protected-document password.
  - `AddImageWatermarkTests` — image-watermark overlay (`image.png` as the watermark source) across DOCX / PDF / XLSX / PPTX, plus opacity + rotation.
  - `SearchWatermarksTests` — asserts the synthetic PDF reports `count: 0`, real samples return a valid `{ count, watermarks: [{ type, text, page, x, y, width, height, rotateAngle }] }` JSON shape, the **add-then-search roundtrip** finds the just-inserted watermark on a DOCX (Office text watermarks reliably surface through `Watermarker.Search()`; the synthetic PDF would be unreliable), and password-protected documents are accepted.
  - `RemoveWatermarksTests` — synthetic PDF reports "No matching watermarks found", **add-then-remove** roundtrip on a DOCX produces a cleaned `<name>_unwatermarked.<ext>` file, and `textFilter` scopes removal to matching watermarks only.
  - `GetDocumentInfoTests` — synthetic PDF reports `pageCount: 1`, real samples return a valid `{ fileName, fileType, fileFormat, size, pageCount, pages: [...] }` JSON shape with per-page dimensions.
  - `ErrorHandlingTests` — unknown filename returns available-files hint; corrupted bytes don't crash the server; `password` parameter is accepted without schema rejection.
- Synthetic-fixture builder writes a minimal bare 1-page PDF at runtime. Real samples (`document.pdf`, `document.docx`, `document.xlsx`, `presentation.pptx`, `image.png`, `protected-document.docx`, `diagram.vsdx`) are committed under `Files/` — copied from the public [`GroupDocs.Watermark-for-.NET` Examples](https://github.com/groupdocs-watermark/GroupDocs.Watermark-for-.NET) repo. The csproj `<None Include="..\..\Files\**\*">` glob copies them to the test output automatically.
- Integration workflow (`.github/workflows/integration.yml`): matrix across `ubuntu-latest`, `windows-latest`, `macos-latest`; nightly cron; `repository_dispatch: nuget-published` listener for release-time smoke tests fired by the main repo's `publish_prod.yml`.
- Linux runners install `libgdiplus libfontconfig1 ttf-mscorefonts-installer` (with the Microsoft EULA pre-accepted via `debconf-set-selections`, plus `fc-cache -f -v` to refresh the font registry). Watermark's `AddWatermark` renders Arial glyphs and needs MS core fonts; bare ubuntu-24.04 does not ship them.
- How-to guides (`how-to/01..06`) cover install-from-NuGet, run-via-Docker, MCP-registry discovery, Claude Desktop config, VS Code / Copilot config, and running this suite locally / in CI.
- Docker-scripts (`docker-scripts/00..04`) provide a copy-pasteable local + containerised test harness.
- Example configs (`examples/claude-desktop.json`, `vscode-mcp.json`, `docker-compose.yml`) pin to 25.6.0.

## Pitfall remediations baked in
- **`ToolCatalog` uses substring keywords that survive the snake_case wire-name transform** (`Resolve("add")` matches `add_watermark`, `Resolve("search")` matches `search_watermarks`). Both keywords are single-word so the underscore-trap (Pitfall #15) doesn't apply, but the convention is preserved.
- **Test fixtures don't assert on `IsError` semantics** — both server-side tools wrap engine exceptions in a descriptive string (`"Watermarking failed for '<file>': ..."` / `"Search failed for '<file>': ..."`), so `result.IsError` is `false` even on engine failure. Tests assert `DoesNotContain("Watermarking failed for", body)` on the success path instead.
- **JSON consumers use `JsonDocument.Parse`** — the `SearchWatermarks` server-side tool returns raw JSON via `JsonSerializer.Serialize(...)` (not piped through `OutputHelper.TruncateText`), so strict-JSON parsing is safe.

## Why
Fourth product Tests repo in the GroupDocs MCP framework family (after Metadata, Conversion, Comparison, Viewer). Validates the shipped NuGet artifact end-to-end on every release and on a nightly cron, and doubles as a reference for users deploying via NuGet, Docker, Claude Desktop, or VS Code.

## Migration / impact
First release — no migration required.
