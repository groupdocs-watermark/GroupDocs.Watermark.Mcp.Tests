using System.Text.Json;
using GroupDocs.Watermark.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Watermark.Mcp.IntegrationTests;

/// SearchWatermarks reads existing watermarks from a document and returns
/// their type, text, page, position, size, and rotation as JSON. The synthetic
/// `SyntheticPdf` has no watermarks (it's a bare 1-page PDF), so search returns
/// `count: 0`. Real samples DO have content that may or may not surface — the
/// JSON-structure assertions accept both empty and non-empty results, while the
/// add-then-search roundtrip verifies the end-to-end write/read path.
[Collection(McpServerCollection.Name)]
public class SearchWatermarksTests
{
    private readonly McpServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SearchWatermarksTests(McpServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task SearchWatermarks_SyntheticPdf_ReturnsZeroWatermarks()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.SearchWatermarks.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.SyntheticPdf },
            });

        if (response.IsError == true)
            throw new InvalidOperationException("Tool reported an error: " + ToolResponse.Text(response));

        var json = ToolResponse.Json(response);
        _output.WriteLine(JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));

        Assert.Equal(JsonValueKind.Object, json.ValueKind);
        Assert.True(json.TryGetProperty("count", out var count));
        Assert.Equal(0, count.GetInt32());

        Assert.True(json.TryGetProperty("watermarks", out var watermarks));
        Assert.Equal(JsonValueKind.Array, watermarks.ValueKind);
        Assert.Equal(0, watermarks.GetArrayLength());
    }

    public static IEnumerable<object[]> RealSamples() => new[]
    {
        new object[] { SampleDocuments.DocumentPdf      },
        new object[] { SampleDocuments.DocumentDocx     },
        new object[] { SampleDocuments.DocumentXlsx     },
        new object[] { SampleDocuments.PresentationPptx },
        new object[] { SampleDocuments.ImagePng         },
        new object[] { SampleDocuments.DiagramVsdx      },
    };

    [Theory]
    [MemberData(nameof(RealSamples))]
    public async Task SearchWatermarks_RealSample_ReturnsValidJsonStructure(string fileName)
    {
        if (!File.Exists(Path.Combine(_fixture.StoragePath, fileName)))
        {
            _output.WriteLine($"Sample '{fileName}' not present in storage — skipping.");
            return;
        }

        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.SearchWatermarks.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = fileName },
            });

        if (response.IsError == true)
            throw new InvalidOperationException(
                $"Search failed for '{fileName}': {ToolResponse.Text(response)}");

        var json = ToolResponse.Json(response);
        _output.WriteLine(JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));

        Assert.True(json.TryGetProperty("count", out var count));
        Assert.Equal(JsonValueKind.Number, count.ValueKind);
        Assert.True(count.GetInt32() >= 0);

        Assert.True(json.TryGetProperty("watermarks", out var watermarks));
        Assert.Equal(JsonValueKind.Array, watermarks.ValueKind);
        Assert.Equal(count.GetInt32(), watermarks.GetArrayLength());

        foreach (var w in watermarks.EnumerateArray())
        {
            Assert.True(w.TryGetProperty("type", out var type));
            Assert.Contains(type.GetString(), new[] { "text", "image" });
            Assert.True(w.TryGetProperty("page", out _));
            Assert.True(w.TryGetProperty("x", out _));
            Assert.True(w.TryGetProperty("y", out _));
        }
    }

    [Fact]
    public async Task AddThenSearch_OnRealDocx_FindsAtLeastOneWatermark()
    {
        // DOCX text watermarks are stored as Office XML elements and are
        // reliably surfaced by Watermarker.Search(). PDF text watermarks may
        // be written as content-stream operations that Search() does not
        // always pick up — so we exercise the roundtrip against a DOCX rather
        // than the synthetic PDF.
        if (!File.Exists(Path.Combine(_fixture.StoragePath, SampleDocuments.DocumentDocx)))
        {
            _output.WriteLine($"Sample '{SampleDocuments.DocumentDocx}' not present — skipping.");
            return;
        }

        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var addResponse = await _fixture.Client.CallToolAsync(
            catalog.AddWatermark.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.DocumentDocx },
                ["text"] = "ROUNDTRIP",
            });

        if (addResponse.IsError == true)
            throw new InvalidOperationException("AddWatermark failed: " + ToolResponse.Text(addResponse));

        Assert.DoesNotContain("Watermarking failed for", ToolResponse.Text(addResponse));

        var searchResponse = await _fixture.Client.CallToolAsync(
            catalog.SearchWatermarks.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = "document_watermarked.docx" },
            });

        if (searchResponse.IsError == true)
            throw new InvalidOperationException(
                "SearchWatermarks failed on watermarked file: " + ToolResponse.Text(searchResponse));

        var json = ToolResponse.Json(searchResponse);
        _output.WriteLine(JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));

        Assert.True(json.TryGetProperty("count", out var count));
        // At least 1 watermark — the one we just added (plus possibly an
        // evaluation watermark if running unlicensed).
        Assert.True(count.GetInt32() >= 1,
            $"Expected at least 1 watermark after AddWatermark on a DOCX; got {count.GetInt32()}.");
    }

    [Fact]
    public async Task SearchWatermarks_ProtectedDocument_AcceptsPasswordArgument()
    {
        if (!File.Exists(Path.Combine(_fixture.StoragePath, SampleDocuments.ProtectedDocumentDocx)))
        {
            _output.WriteLine($"Sample '{SampleDocuments.ProtectedDocumentDocx}' not present — skipping.");
            return;
        }

        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.SearchWatermarks.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.ProtectedDocumentDocx },
                ["password"] = SampleDocuments.ProtectedDocumentPassword,
            });

        if (response.IsError == true)
            throw new InvalidOperationException(
                $"Protected-document search failed: {ToolResponse.Text(response)}");

        var json = ToolResponse.Json(response);
        _output.WriteLine(JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));

        Assert.True(json.TryGetProperty("count", out _));
        Assert.True(json.TryGetProperty("watermarks", out _));
    }
}
