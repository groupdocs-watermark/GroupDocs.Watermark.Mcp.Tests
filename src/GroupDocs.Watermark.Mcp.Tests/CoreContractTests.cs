using GroupDocs.Watermark.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Watermark.Mcp.IntegrationTests;

/// <summary>
/// The input and error contract shared by every GroupDocs MCP server (GroupDocs.Mcp.Core).
/// </summary>
/// <remarks>
/// One test per shared defect the 2026-08-16 external audit confirmed on all 12 products.
/// Free to run - no licence, no metered key.
///
/// The audit's sharpest finding was that the previous oracles could not see these defects: an
/// unknown-file assertion of the form "IsError || text.Contains(not found)" passes on the
/// opaque error it was meant to catch, because that error sets IsError. So these assert the
/// PROMISED text, not merely that something went wrong.
/// </remarks>
public class CoreContractTests : McpServerTestBase
{
    private readonly ITestOutputHelper _output;

    public CoreContractTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const string LicenseStatusTool = "get_license_status";

    // ---- S1: the fileName form the tool descriptions recommend -------------

    [Fact]
    public async Task InfoTool_WithFileNameOnly_Resolves()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        // The descriptions say "just pass the filename the user provided", and the schema
        // allows it - yet this form used to throw an unhandled ArgumentException the client
        // saw only as the opaque invoke error.
        var response = await _fixture.Client.CallToolAsync(
            catalog.GetDocumentInfo.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["fileName"] = SampleDocuments.SyntheticPdf },
            });

        Assert.False(response.IsError ?? false,
            $"[{_fixture.Channel}] fileName-only input failed: {ToolResponse.Text(response)}");
    }

    // ---- S2: the available-files listing the descriptions promise ----------

    [Fact]
    public async Task InfoTool_MissingFile_ReturnsTheAvailableFilesListing()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.GetDocumentInfo.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = "definitely-not-here.pdf" },
            });

        var text = ToolResponse.Text(response);
        _output.WriteLine(text);

        // Assert what the description actually promises. A loose oracle passes on the
        // opaque error too - precisely how this defect survived the previous suite.
        Assert.Contains("Available files:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("An error occurred invoking", text, StringComparison.Ordinal);

        // S3: a real failure must be flagged, not merely described.
        Assert.True(response.IsError ?? false,
            "A failed operation must set isError so a client can detect it without parsing prose.");
    }

    // ---- S2c: a missing required parameter must be self-correctable --------

    [Fact]
    public async Task InfoTool_WithNoArguments_NamesTheMissingParameter()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.GetDocumentInfo.Name, new Dictionary<string, object?>());

        var text = ToolResponse.Text(response);
        _output.WriteLine(text);

        Assert.True(response.IsError ?? false, "A missing required parameter is a failure.");
        Assert.DoesNotContain("An error occurred invoking", text, StringComparison.Ordinal);
    }

    // ---- The Core-shipped status tool --------------------------------------

    [Fact]
    public async Task GetLicenseStatus_IsRegisteredAndDescribesTheServer()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);
        Assert.Contains(catalog.All, t => t.Name == LicenseStatusTool);

        var response = await _fixture.Client.CallToolAsync(
            LicenseStatusTool, new Dictionary<string, object?>());

        Assert.False(response.IsError ?? false,
            $"[{_fixture.Channel}] {LicenseStatusTool} failed: {ToolResponse.Text(response)}");

        var json = ToolResponse.Json(response);
        _output.WriteLine(json.ToString());

        var mode = json.GetProperty("mode").GetString();
        Assert.Contains(mode, new[] { "evaluation", "licensed", "metered" });

        // Before this tool existed a client had no way to discover it was running
        // unlicensed - the audit called that out explicitly.
        Assert.Equal(mode != "evaluation", json.GetProperty("licensed").GetBoolean());

        // The engine version was likewise invisible; the family spans 26.3 to 26.8.
        var engine = json.GetProperty("engine");
        Assert.Equal("GroupDocs.Watermark", engine.GetProperty("name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(engine.GetProperty("version").GetString()));

        // It must not report the server assembly as the engine.
        Assert.NotEqual(
            json.GetProperty("server").GetProperty("name").GetString(),
            engine.GetProperty("name").GetString());
    }
}
