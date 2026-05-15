using System.Text.Json;
using GroupDocs.Watermark.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Watermark.Mcp.IntegrationTests;

/// GetDocumentInfo reads file metadata (type, page count, per-page dimensions)
/// without modifying the document. Should work in evaluation mode without
/// license. Tests assert the JSON shape but not specific values, since real
/// sample contents may drift across upstream refreshes.
[Collection(McpServerCollection.Name)]
public class GetDocumentInfoTests
{
    private readonly McpServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public GetDocumentInfoTests(McpServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task GetDocumentInfo_SyntheticPdf_ReturnsOnePage()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.GetDocumentInfo.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.SyntheticPdf },
            });

        if (response.IsError == true)
            throw new InvalidOperationException("Tool reported an error: " + ToolResponse.Text(response));

        var json = ToolResponse.Json(response);
        _output.WriteLine(JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));

        Assert.Equal(JsonValueKind.Object, json.ValueKind);
        Assert.True(json.TryGetProperty("fileName", out var fileName));
        Assert.Equal(SampleDocuments.SyntheticPdf, fileName.GetString());

        Assert.True(json.TryGetProperty("fileFormat", out var fileFormat));
        Assert.Equal(".pdf", fileFormat.GetString());

        Assert.True(json.TryGetProperty("pageCount", out var pageCount));
        Assert.Equal(1, pageCount.GetInt32());
    }

    public static IEnumerable<object[]> RealSamples() => new[]
    {
        new object[] { SampleDocuments.DocumentPdf,      ".pdf"  },
        new object[] { SampleDocuments.DocumentDocx,     ".docx" },
        new object[] { SampleDocuments.DocumentXlsx,     ".xlsx" },
        new object[] { SampleDocuments.PresentationPptx, ".pptx" },
        new object[] { SampleDocuments.ImagePng,         ".png"  },
        new object[] { SampleDocuments.DiagramVsdx,      ".vsdx" },
    };

    [Theory]
    [MemberData(nameof(RealSamples))]
    public async Task GetDocumentInfo_RealSample_ReturnsValidJsonStructure(string fileName, string expectedExtension)
    {
        if (!File.Exists(Path.Combine(_fixture.StoragePath, fileName)))
        {
            _output.WriteLine($"Sample '{fileName}' not present in storage — skipping.");
            return;
        }

        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.GetDocumentInfo.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = fileName },
            });

        if (response.IsError == true)
            throw new InvalidOperationException(
                $"GetDocumentInfo failed for '{fileName}': {ToolResponse.Text(response)}");

        var json = ToolResponse.Json(response);
        _output.WriteLine(JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));

        Assert.True(json.TryGetProperty("fileName", out var fileNameProp));
        Assert.Equal(fileName, fileNameProp.GetString());

        Assert.True(json.TryGetProperty("fileFormat", out var fileFormat));
        Assert.Equal(expectedExtension, fileFormat.GetString(), ignoreCase: true);

        Assert.True(json.TryGetProperty("size", out var size));
        Assert.True(size.GetInt64() > 0);

        // pageCount may be null for some formats (raw images) — that's valid.
        Assert.True(json.TryGetProperty("pageCount", out _));
    }
}
