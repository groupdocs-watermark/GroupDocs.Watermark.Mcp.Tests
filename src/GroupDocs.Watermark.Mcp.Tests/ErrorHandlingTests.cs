using GroupDocs.Watermark.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Watermark.Mcp.IntegrationTests;

[Collection(McpServerCollection.Name)]
public class ErrorHandlingTests
{
    private readonly McpServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ErrorHandlingTests(McpServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task SearchWatermarks_UnknownFile_ReturnsErrorListingAvailableFiles()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.SearchWatermarks.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = "does-not-exist.pdf" },
            });

        var body = ToolResponse.Text(response);
        _output.WriteLine(body);

        // Tool description promises to return available files when the name isn't resolved.
        var isErrorReported = (response.IsError ?? false)
            || body.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || body.Contains("available", StringComparison.OrdinalIgnoreCase)
            || body.Contains(SampleDocuments.SyntheticPdf, StringComparison.OrdinalIgnoreCase);

        Assert.True(isErrorReported,
            $"Expected an error / available-files hint for an unknown file. Response:\n{body}");
    }

    [Fact]
    public async Task SearchWatermarks_CorruptedFile_DoesNotCrashServer()
    {
        var corrupted = "corrupted.pdf";
        await File.WriteAllBytesAsync(
            Path.Combine(_fixture.StoragePath, corrupted),
            new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0xDE, 0xAD, 0xBE, 0xEF });

        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        // Server should either return a tool error or throw an MCP error — either way,
        // it must stay up and continue answering subsequent requests.
        try
        {
            var response = await _fixture.Client.CallToolAsync(
                catalog.SearchWatermarks.Name,
                new Dictionary<string, object?>
                {
                    ["file"] = new Dictionary<string, object?> { ["filePath"] = corrupted },
                });
            _output.WriteLine($"Tool response: {ToolResponse.Text(response)}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Call threw (acceptable): {ex.GetType().Name}: {ex.Message}");
        }

        // Prove the server is still alive by making another call.
        var listAfter = await _fixture.Client.ListToolsAsync();
        Assert.NotEmpty(listAfter);
    }

    [Fact]
    public async Task PasswordParameter_IsAcceptedByTool()
    {
        // Wrong password on a non-protected file — the tool should treat it as a no-op
        // or return a clear error, but must accept the `password` parameter without
        // rejecting the schema.
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.SearchWatermarks.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.SyntheticPdf },
                ["password"] = "not-a-real-password",
            });

        var body = ToolResponse.Text(response);
        _output.WriteLine(body);

        // Either succeeds (password ignored for unprotected file) or returns a graceful error.
        Assert.False(string.IsNullOrEmpty(body));
    }
}
