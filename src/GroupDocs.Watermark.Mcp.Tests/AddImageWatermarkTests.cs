using GroupDocs.Watermark.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Watermark.Mcp.IntegrationTests;

/// AddImageWatermark overlays an image (logo, signature scan, stamp) on a
/// document. The image source and target are both resolved from the server's
/// storage path. Uses `image.png` from the real-samples set as the watermark
/// image for all tests — same logo overlaid on different target formats.
public class AddImageWatermarkTests : McpServerTestBase
{
    private readonly ITestOutputHelper _output;

    public AddImageWatermarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IEnumerable<object[]> TargetsForImageWatermark() => new[]
    {
        new object[] { SampleDocuments.DocumentPdf,      "document_watermarked.pdf"      },
        new object[] { SampleDocuments.DocumentDocx,     "document_watermarked.docx"     },
        new object[] { SampleDocuments.DocumentXlsx,     "document_watermarked.xlsx"     },
        new object[] { SampleDocuments.PresentationPptx, "presentation_watermarked.pptx" },
    };

    [Theory]
    [MemberData(nameof(TargetsForImageWatermark))]
    public async Task AddImageWatermark_RealSample_ProducesWatermarkedFile(string fileName, string expectedOutputName)
    {
        if (!File.Exists(Path.Combine(_fixture.StoragePath, fileName))
            || !File.Exists(Path.Combine(_fixture.StoragePath, SampleDocuments.ImagePng)))
        {
            _output.WriteLine($"Sample '{fileName}' or watermark image '{SampleDocuments.ImagePng}' not present — skipping.");
            return;
        }

        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.AddImageWatermark.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = fileName },
                ["watermarkImage"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.ImagePng },
                ["opacity"] = 0.4,
            });

        if (response.IsError == true)
            throw new InvalidOperationException(
                $"Image watermarking failed for '{fileName}': {ToolResponse.Text(response)}");

        var body = ToolResponse.Text(response);
        _output.WriteLine(body);

        Assert.DoesNotContain("Image watermarking failed for", body);

        var watermarkedPath = Path.Combine(_fixture.StoragePath, expectedOutputName);
        Assert.True(File.Exists(watermarkedPath),
            $"Expected watermarked file at '{watermarkedPath}'. Response body:\n{body}");

        Assert.Contains("Added image watermark", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddImageWatermark_CustomOpacityAndRotation_AcceptsAllArguments()
    {
        if (!File.Exists(Path.Combine(_fixture.StoragePath, SampleDocuments.DocumentPdf))
            || !File.Exists(Path.Combine(_fixture.StoragePath, SampleDocuments.ImagePng)))
        {
            _output.WriteLine("Required samples not present — skipping.");
            return;
        }

        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.AddImageWatermark.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.DocumentPdf },
                ["watermarkImage"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.ImagePng },
                ["opacity"] = 0.25,
                ["rotation"] = -15,
            });

        if (response.IsError == true)
            throw new InvalidOperationException("Tool reported an error: " + ToolResponse.Text(response));

        var body = ToolResponse.Text(response);
        _output.WriteLine(body);

        Assert.DoesNotContain("Image watermarking failed for", body);
    }
}
