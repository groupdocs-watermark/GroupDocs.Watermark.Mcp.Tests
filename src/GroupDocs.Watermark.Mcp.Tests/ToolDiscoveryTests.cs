using GroupDocs.Watermark.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Watermark.Mcp.IntegrationTests;

public class ToolDiscoveryTests : McpServerTestBase
{
    private readonly ITestOutputHelper _output;

    public ToolDiscoveryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ServerInfo_AdvertisesGroupDocsWatermarkMcp()
    {
        var info = _fixture.Client.ServerInfo;

        Assert.NotNull(info);
        Assert.Equal("GroupDocs.Watermark.Mcp", info!.Name);
        Assert.False(string.IsNullOrWhiteSpace(info.Version));

        _output.WriteLine($"Server: {info.Name} {info.Version}  (package under test: {_fixture.PackageVersionUnderTest})");
    }

    [Fact]
    public async Task ListTools_ExposesAllFiveWatermarkTools()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        foreach (var tool in catalog.All)
            _output.WriteLine($"tool: {tool.Name} — {tool.Description}");

        // 5 product tools plus get_license_status, which GroupDocs.Mcp.Core
        // registers on every server from 26.9.0.
        Assert.Equal(6, catalog.All.Count);
        Assert.NotNull(catalog.AddWatermark);
        Assert.NotNull(catalog.AddImageWatermark);
        Assert.NotNull(catalog.SearchWatermarks);
        Assert.NotNull(catalog.RemoveWatermarks);
        Assert.NotNull(catalog.GetDocumentInfo);
    }

    [Fact]
    public async Task AllTools_HaveNonEmptyDescriptionAndInputSchema()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        Assert.NotEmpty(catalog.All);
        foreach (var tool in catalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Description),
                $"Tool '{tool.Name}' has no description.");

            var schema = tool.JsonSchema;
            Assert.True(schema.ValueKind == System.Text.Json.JsonValueKind.Object,
                $"Tool '{tool.Name}' has no object input schema.");
            Assert.True(schema.TryGetProperty("properties", out _),
                $"Tool '{tool.Name}' schema missing 'properties'.");
        }
    }
}
