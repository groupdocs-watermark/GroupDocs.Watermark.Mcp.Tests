# Run the integration tests

This repo's test suite validates the **published** `GroupDocs.Watermark.Mcp`
NuGet package end-to-end — it spawns the server via `dnx`, connects as an MCP
client, and exercises every advertised tool. Useful when you want to confirm a
release is healthy before promoting it, or gate CI on live smoke checks.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Network access to nuget.org (the first run downloads the package)
- Optional: a GroupDocs license file to unlock the licensed-mode tests

## Run locally

```bash
# All 12 tests against the default pinned version (26.4.4)
dotnet test -c Release
```

## Run against a different published version

```bash
# Via MSBuild property
dotnet test -c Release -p:McpPackageVersion=26.4.4

# Or via env var
MCP_PACKAGE_VERSION=26.4.4 dotnet test -c Release
```

Version resolution order (highest wins):

1. `MCP_PACKAGE_VERSION` environment variable
2. `McpPackageVersion` MSBuild property → baked into assembly metadata
3. Default: `26.4.4`

## Unlock licensed-mode tests

Three tests in `AddWatermarkTests` only assert success when a GroupDocs
license is configured. Without one, they no-op (and the evaluation-mode test
runs instead).

```bash
export GROUPDOCS_LICENSE_PATH=/absolute/path/to/GroupDocs.Total.lic
dotnet test -c Release
```

The license path is forwarded into the server child process by
`McpServerFixture`.

## Run a subset

```bash
# Only discovery (fastest — no tool invocations after handshake)
dotnet test -c Release --filter "FullyQualifiedName~ToolDiscovery"

# Only watermark-search tests
dotnet test -c Release --filter "FullyQualifiedName~SearchWatermarks"

# Only watermark-add tests
dotnet test -c Release --filter "FullyQualifiedName~AddWatermark"

# Only error-handling tests
dotnet test -c Release --filter "FullyQualifiedName~ErrorHandling"
```

## Expected output

```
Passed  ToolDiscoveryTests.ServerInfo_AdvertisesGroupDocsWatermarkMcp
Passed  ToolDiscoveryTests.ListTools_ExposesAddWatermarkAndSearchWatermarks
Passed  ToolDiscoveryTests.AllTools_HaveNonEmptyDescriptionAndInputSchema
Passed  AddWatermarkTests.AddWatermark_AuthoredPdf_ProducesWatermarkedFile
Passed  AddWatermarkTests.AddWatermark_CustomFontAndRotation_AcceptsAllArguments
Passed  AddWatermarkTests.AddWatermark_RealSample_ProducesWatermarkedFile  (parameterised × 5 real samples)
Passed  SearchWatermarksTests.SearchWatermarks_AuthoredPdf_ReturnsZeroWatermarks
Passed  SearchWatermarksTests.SearchWatermarks_RealSample_ReturnsValidJsonStructure  (parameterised × 5 real samples)
Passed  SearchWatermarksTests.AddThenSearch_FindsPreviouslyAddedWatermark
Passed  ErrorHandlingTests.SearchWatermarks_UnknownFile_ReturnsErrorListingAvailableFiles
Passed  ErrorHandlingTests.SearchWatermarks_CorruptedFile_DoesNotCrashServer
Passed  ErrorHandlingTests.PasswordParameter_IsAcceptedByTool

Total: ~16 (including parameterised cases), Passed: all, Time: ~30s
```

The first test run is slower (~60s) because `dnx` downloads the package into
the NuGet cache.

## Add real-world fixtures

Synthetic PDF/JPEG fixtures ([SampleDocuments.cs](../src/GroupDocs.Watermark.Mcp.Tests/Fixtures/SampleDocuments.cs))
exercise the core pathways. To test format-specific behaviour (DOCX, XLSX,
MP3, TIFF, etc.):

1. Drop the file into [sample-docs/](../sample-docs/). The csproj's
   `<None Include="..\..\sample-docs\**\*" CopyToOutputDirectory="PreserveNewest" />`
   glob copies it to the test output, which `McpServerFixture` seeds into the
   server's storage path.
2. Add a test referencing it by filename:

```csharp
var response = await _fixture.Client.CallToolAsync(
    catalog.Read.Name,
    new Dictionary<string, object?>
    {
        ["file"] = new Dictionary<string, object?> { ["filePath"] = "contract.docx" },
    });
```

3. Ensure the file is license-clean (self-authored or CC0 / Apache-2.0) before
   committing.

## Use in CI

The workflow at [.github/workflows/integration.yml](../.github/workflows/integration.yml)
runs on four triggers:

- **`push` + `pull_request`** — validates repo changes.
- **Nightly cron** (`0 6 * * *` UTC) — catches regressions in nuget.org, `dnx`,
  or the .NET runtime.
- **`workflow_dispatch`** with a `package_version` input — smoke-test any
  published version manually.
- **`repository_dispatch`** (`nuget-published` event) — fires from the main
  repo's publish pipeline after `dotnet nuget push`. Payload:
  `{ "package_version": "x.y.z" }`.

Matrix: `ubuntu-latest`, `windows-latest`, `macos-latest`.

### Wire the release-smoke hook in the server repo

Add this step to the server repo's publish workflow, right after the push step:

```yaml
- name: Dispatch integration tests
  env:
    GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
  run: |
    gh api \
      repos/groupdocs-watermark/GroupDocs.Watermark.Mcp.Tests/dispatches \
      -f event_type=nuget-published \
      -f 'client_payload[package_version]=${{ steps.version.outputs.version }}'
```

The `GITHUB_TOKEN` scope is enough if both repos are in the same org.
Otherwise use a fine-grained PAT with `Contents: write` on the test repo.

### License secret in CI

Store a base64-encoded `.lic` file as the repo secret `GROUPDOCS_LICENSE`.
The workflow decodes it into `$RUNNER_TEMP` and exports `GROUPDOCS_LICENSE_PATH`
— the licensed-mode tests then run automatically.

```bash
# Locally: base64-encode and set the secret
base64 -w0 GroupDocs.Total.lic | gh secret set GROUPDOCS_LICENSE \
  --repo groupdocs-watermark/GroupDocs.Watermark.Mcp.Tests
```

## Debugging failures

### Inspect server stderr

`McpServerFixture` doesn't currently capture the child process's stderr — if a
test fails with a cryptic `"An error occurred invoking 'add_watermark'"`,
reproduce the call manually:

```bash
mkdir -p /tmp/gd && cp some.pdf /tmp/gd/
(
  echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"p","version":"1"}}}'
  echo '{"jsonrpc":"2.0","method":"notifications/initialized"}'
  echo '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"add_watermark","arguments":{"file":{"filePath":"some.pdf"}}}}'
  sleep 5
) | GROUPDOCS_MCP_STORAGE_PATH=/tmp/gd dnx GroupDocs.Watermark.Mcp@26.4.4 --yes \
    > stdout.log 2> stderr.log
tail -50 stderr.log
```

The server logs full exception stacks to stderr.

### Verbose test output

```bash
dotnet test -c Release --logger "console;verbosity=detailed"
```

## Troubleshooting

| Symptom | Fix |
|---|---|
| `dnx: command not found` during test | Ensure .NET 10 SDK is installed. On Windows, `CommandResolver` looks for `dnx.cmd`; check it exists at `C:\Program Files\dotnet\dnx.cmd`. |
| All AddWatermark tests fail | Running unlicensed. Expected — `AddWatermark_InEvaluationMode_ReturnsErrorResponse` should pass, the `*_Licensed` tests no-op. If the eval-mode test also fails, check the package actually installed. |
| First run takes minutes | NuGet download. Subsequent runs hit the cache. |
| Cross-OS flakes in CI | Different line endings in sample-docs fixtures. Commit with `text=auto` or binary mode in `.gitattributes`. |

## Next steps

- [03 — MCP registry](03-verify-mcp-registry.md) — cross-check registry state
- [01 — NuGet install](01-install-from-nuget.md) — manual smoke the same way users do
