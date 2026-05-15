# GroupDocs.Watermark.Mcp.Tests

Integration tests for the [`GroupDocs.Watermark.Mcp`](https://www.nuget.org/packages/GroupDocs.Watermark.Mcp)
NuGet package — an MCP server that exposes
[GroupDocs.Watermark](https://products.groupdocs.com/watermark) as AI-callable tools.

This repository validates the **published** NuGet artifact end-to-end: it
launches the server via `dnx`, connects as an MCP client, and exercises every
advertised tool. It also doubles as a copy-pasteable set of example configs
and user-facing how-to guides for every deployment channel.

## Documentation

- [how-to/](how-to/) — step-by-step guides for every deployment channel
  ([NuGet](how-to/01-install-from-nuget.md),
  [Docker](how-to/02-run-via-docker.md),
  [MCP registry](how-to/03-verify-mcp-registry.md),
  [Claude Desktop](how-to/04-use-with-claude-desktop.md),
  [VS Code / Copilot](how-to/05-use-with-vscode-copilot.md),
  [running the tests](how-to/06-run-integration-tests.md)).
- [examples/](examples/) — ready-to-paste `claude-desktop.json`,
  `vscode-mcp.json`, and `docker-compose.yml`.
- [AGENTS.md](AGENTS.md) — orientation for AI coding agents working in this repo.
- [llms.txt](llms.txt) — machine-readable summary for LLM tooling.
- [changelog/](changelog/) — one entry per change set (see
  [changelog/README.md](changelog/README.md) for format).

## What gets tested

| Area | Covered by |
|---|---|
| Package installs and starts via `dnx` | [McpServerFixture](src/GroupDocs.Watermark.Mcp.Tests/Fixtures/McpServerFixture.cs) |
| MCP handshake, server info, tool list (5 tools) | [ToolDiscoveryTests](src/GroupDocs.Watermark.Mcp.Tests/ToolDiscoveryTests.cs) |
| `add_watermark` — synthetic PDF + 6 real samples (PDF/DOCX/XLSX/PPTX/PNG/VSDX) + custom font/rotation + password | [AddWatermarkTests](src/GroupDocs.Watermark.Mcp.Tests/AddWatermarkTests.cs) |
| `add_image_watermark` — image overlay across formats | [AddImageWatermarkTests](src/GroupDocs.Watermark.Mcp.Tests/AddImageWatermarkTests.cs) |
| `search_watermarks` — zero-watermark + real-sample JSON shape + add-then-search roundtrip + password | [SearchWatermarksTests](src/GroupDocs.Watermark.Mcp.Tests/SearchWatermarksTests.cs) |
| `remove_watermarks` — strip-all + text-filter + roundtrip | [RemoveWatermarksTests](src/GroupDocs.Watermark.Mcp.Tests/RemoveWatermarksTests.cs) |
| `get_document_info` — file type + page count + per-page dimensions | [GetDocumentInfoTests](src/GroupDocs.Watermark.Mcp.Tests/GetDocumentInfoTests.cs) |
| Unknown / corrupted files, password parameter | [ErrorHandlingTests](src/GroupDocs.Watermark.Mcp.Tests/ErrorHandlingTests.cs) |

## Running locally

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet test
```

Test a specific published version:

```bash
dotnet test -p:McpPackageVersion=26.5.0
# or
MCP_PACKAGE_VERSION=26.5.0 dotnet test
```

The first run downloads the NuGet package — subsequent runs are cached.

## CI

[.github/workflows/integration.yml](.github/workflows/integration.yml) runs on:

- Every push / PR.
- Nightly cron — catches regressions in nuget.org, the dnx shim, or the .NET runtime.
- `workflow_dispatch` with a `package_version` input — manual smoke of any version.
- `repository_dispatch` (`nuget-published`) — fires from the main repo's publish pipeline
  so every release is smoke-tested against live nuget.org. See
  [Release smoke hook](#release-smoke-hook).

Matrix: `ubuntu-latest`, `windows-latest`, `macos-latest`. Linux runners
install `libgdiplus libfontconfig1 ttf-mscorefonts-installer` (with the
Microsoft EULA pre-accepted via `debconf-set-selections`, plus `fc-cache -f -v`
to refresh the font registry). `AddWatermark` renders Arial glyphs and needs
MS core fonts; bare ubuntu-24.04 does not ship them.

## Release smoke hook

To auto-verify each release, add this step to the main repo's publish workflow
after the `dotnet nuget push` step:

```yaml
- name: Dispatch smoke tests
  env:
    GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
  run: |
    gh api repos/groupdocs-watermark/GroupDocs.Watermark.Mcp.Tests/dispatches \
      -f event_type=nuget-published \
      -f 'client_payload[package_version]=${{ steps.version.outputs.version }}'
```

## Evaluation vs licensed mode

Unlike `GroupDocs.Metadata.Save()` which throws in evaluation mode,
`GroupDocs.Watermark` runs successfully without a license — it simply adds an
additional evaluation watermark alongside the user-requested one. Tests
therefore run identically with or without `GROUPDOCS_LICENSE_PATH`; the
license only affects whether the response text carries an eval-mode banner.

For CI, store a base64-encoded `.lic` file as repo secret `GROUPDOCS_LICENSE`
— the workflow decodes it into `$RUNNER_TEMP` and exports
`GROUPDOCS_LICENSE_PATH` automatically. Useful for verifying the unlicensed
banner is suppressed.

## Fixture documents

Sample documents are built from byte-arrays in
[SampleDocuments.cs](src/GroupDocs.Watermark.Mcp.Tests/Fixtures/SampleDocuments.cs)
at test startup — no binary files are checked into this repo.

To add a real-world fixture, drop it into [sample-docs/](sample-docs/) and write
a test that references it by filename. The project auto-copies everything in
`sample-docs/` to the test output.

## License

MIT — see [LICENSE](LICENSE)
