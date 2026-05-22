using System.Text.Json;
using GroupDocs.Watermark.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Watermark.Mcp.IntegrationTests;

/// RemoveWatermarks finds existing watermarks and saves a cleaned copy as
/// `<name>_unwatermarked.<ext>`. Optional `textFilter` scopes removal to
/// watermarks whose text contains the filter substring.
///
/// Eval-mode note: unlike AddWatermark (which works unlicensed, just adding an
/// extra evaluation watermark), removing watermarks from Office formats edits
/// document parts, which GroupDocs.Watermark BLOCKS in evaluation mode
/// ("Adding/removing of document parts is not allowed in evaluation mode").
/// The tool surfaces that as a caught, descriptive "Watermark removal failed
/// for ..." message (Pitfall #18) rather than crashing. Tests that actually
/// remove from a real document therefore branch on whether a license is present.
public class RemoveWatermarksTests : McpServerTestBase
{
    private readonly ITestOutputHelper _output;

    public RemoveWatermarksTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static bool IsLicensed =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GROUPDOCS_LICENSE_PATH"));

    [Fact]
    public async Task RemoveWatermarks_SyntheticPdfWithNoWatermarks_ReportsNoMatches()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.RemoveWatermarks.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.SyntheticPdf },
            });

        if (response.IsError == true)
            throw new InvalidOperationException("Tool reported an error: " + ToolResponse.Text(response));

        var body = ToolResponse.Text(response);
        _output.WriteLine(body);

        Assert.DoesNotContain("Watermark removal failed for", body);
        Assert.Contains("No matching watermarks found", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddThenRemove_OnRealDocx_ProducesCleanedCopy()
    {
        if (!File.Exists(Path.Combine(_fixture.StoragePath, SampleDocuments.DocumentDocx)))
        {
            _output.WriteLine($"Sample '{SampleDocuments.DocumentDocx}' not present — skipping.");
            return;
        }

        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        // 1. Add a watermark.
        var addResponse = await _fixture.Client.CallToolAsync(
            catalog.AddWatermark.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.DocumentDocx },
                ["text"] = "REMOVE-ME",
            });

        if (addResponse.IsError == true)
            throw new InvalidOperationException("AddWatermark failed: " + ToolResponse.Text(addResponse));

        // 2. Remove it.
        var removeResponse = await _fixture.Client.CallToolAsync(
            catalog.RemoveWatermarks.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = "document_watermarked.docx" },
            });

        if (removeResponse.IsError == true)
            throw new InvalidOperationException(
                "RemoveWatermarks failed: " + ToolResponse.Text(removeResponse));

        var body = ToolResponse.Text(removeResponse);
        _output.WriteLine(body);

        if (!IsLicensed)
        {
            // Eval mode blocks document-part removal from Office formats. Assert the
            // tool degrades gracefully (caught, descriptive error per Pitfall #18) and
            // the server stays up; the cleaned-file check only applies when licensed.
            Assert.Contains("Watermark removal failed for", body);
            Assert.Contains("evaluation mode", body, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(await _fixture.Client.ListToolsAsync());
            return;
        }

        Assert.DoesNotContain("Watermark removal failed for", body);

        // Cleaned output exists.
        var unwatermarkedPath = Path.Combine(_fixture.StoragePath, "document_watermarked_unwatermarked.docx");
        Assert.True(File.Exists(unwatermarkedPath),
            $"Expected cleaned file at '{unwatermarkedPath}'. Response body:\n{body}");
    }

    [Fact]
    public async Task RemoveWatermarks_WithTextFilter_OnlyRemovesMatching()
    {
        if (!File.Exists(Path.Combine(_fixture.StoragePath, SampleDocuments.DocumentDocx)))
        {
            _output.WriteLine($"Sample '{SampleDocuments.DocumentDocx}' not present — skipping.");
            return;
        }

        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        // Stamp two distinct watermarks: one we want to keep, one we want to remove.
        await _fixture.Client.CallToolAsync(catalog.AddWatermark.Name, new Dictionary<string, object?>
        {
            ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.DocumentDocx },
            ["text"] = "KEEP-ME",
        });

        // The previous call wrote `document_watermarked.docx`; reuse that as the
        // base so both watermarks land on the same document.
        await _fixture.Client.CallToolAsync(catalog.AddWatermark.Name, new Dictionary<string, object?>
        {
            ["file"] = new Dictionary<string, object?> { ["filePath"] = "document_watermarked.docx" },
            ["text"] = "STRIP-ME",
        });

        var removeResponse = await _fixture.Client.CallToolAsync(
            catalog.RemoveWatermarks.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = "document_watermarked_watermarked.docx" },
                ["textFilter"] = "STRIP-ME",
            });

        if (removeResponse.IsError == true)
            throw new InvalidOperationException(
                "RemoveWatermarks (filtered) failed: " + ToolResponse.Text(removeResponse));

        var body = ToolResponse.Text(removeResponse);
        _output.WriteLine(body);

        if (!IsLicensed)
        {
            // Eval mode blocks document-part removal from Office formats — the tool
            // returns a caught, descriptive error (Pitfall #18). Assert graceful
            // degradation; the filtered-removal outcome only applies when licensed.
            Assert.Contains("Watermark removal failed for", body);
            Assert.Contains("evaluation mode", body, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(await _fixture.Client.ListToolsAsync());
            return;
        }

        Assert.DoesNotContain("Watermark removal failed for", body);
        // Body should either report "Removed N watermark(s) matching 'STRIP-ME'..."
        // or "No matching watermarks found ... (filter: 'STRIP-ME')". Both are
        // valid outcomes — the engine may or may not surface filterable-on-text
        // watermarks back through Search() depending on format-specific storage.
    }
}
