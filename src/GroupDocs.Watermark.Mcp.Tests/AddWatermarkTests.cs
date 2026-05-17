using GroupDocs.Watermark.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Watermark.Mcp.IntegrationTests;

/// AddWatermark renders a text watermark onto a document and saves a copy as
/// `<name>_watermarked.<ext>` to storage. GroupDocs.Watermark runs successfully
/// in evaluation mode (it just adds an additional evaluation watermark
/// alongside the user-requested one), so tests run identically with or without
/// GROUPDOCS_LICENSE_PATH — license only affects the eval-mode banner prefix.
public class AddWatermarkTests : McpServerTestBase
{
    private readonly ITestOutputHelper _output;

    public AddWatermarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task AddWatermark_SyntheticPdf_ProducesWatermarkedFile()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.AddWatermark.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.SyntheticPdf },
                ["text"] = "DRAFT",
            });

        if (response.IsError == true)
            throw new InvalidOperationException("Tool reported an error: " + ToolResponse.Text(response));

        var body = ToolResponse.Text(response);
        _output.WriteLine(body);

        Assert.DoesNotContain("Watermarking failed for", body);

        var watermarkedPath = Path.Combine(_fixture.StoragePath, "synthetic_watermarked.pdf");
        Assert.True(File.Exists(watermarkedPath),
            $"Expected watermarked file at '{watermarkedPath}'. Response body:\n{body}");

        Assert.Contains("Added text watermark", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DRAFT", body);
    }

    public static IEnumerable<object[]> RealSamplesForWatermarking() => new[]
    {
        new object[] { SampleDocuments.DocumentPdf,      "document_watermarked.pdf"      },
        new object[] { SampleDocuments.DocumentDocx,     "document_watermarked.docx"     },
        new object[] { SampleDocuments.DocumentXlsx,     "document_watermarked.xlsx"     },
        new object[] { SampleDocuments.PresentationPptx, "presentation_watermarked.pptx" },
        new object[] { SampleDocuments.ImagePng,         "image_watermarked.png"         },
        new object[] { SampleDocuments.DiagramVsdx,      "diagram_watermarked.vsdx"      },
    };

    [Theory]
    [MemberData(nameof(RealSamplesForWatermarking))]
    public async Task AddWatermark_RealSample_ProducesWatermarkedFile(string fileName, string expectedOutputName)
    {
        if (!File.Exists(Path.Combine(_fixture.StoragePath, fileName)))
        {
            _output.WriteLine($"Sample '{fileName}' not present in storage — skipping.");
            return;
        }

        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.AddWatermark.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = fileName },
                ["text"] = "CONFIDENTIAL",
            });

        if (response.IsError == true)
            throw new InvalidOperationException(
                $"Watermarking failed for '{fileName}': {ToolResponse.Text(response)}");

        var body = ToolResponse.Text(response);
        _output.WriteLine(body);

        Assert.DoesNotContain("Watermarking failed for", body);

        var watermarkedPath = Path.Combine(_fixture.StoragePath, expectedOutputName);
        Assert.True(File.Exists(watermarkedPath),
            $"Expected watermarked file at '{watermarkedPath}'. Response body:\n{body}");
    }

    [Fact]
    public async Task AddWatermark_CustomFontAndRotation_AcceptsAllArguments()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.AddWatermark.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.SyntheticPdf },
                ["text"] = "TOP SECRET",
                ["fontSize"] = 48,
                ["rotation"] = -30,
            });

        if (response.IsError == true)
            throw new InvalidOperationException("Tool reported an error: " + ToolResponse.Text(response));

        var body = ToolResponse.Text(response);
        _output.WriteLine(body);

        Assert.DoesNotContain("Watermarking failed for", body);
        Assert.Contains("TOP SECRET", body);
    }

    [Fact]
    public async Task AddWatermark_ProtectedDocument_AcceptsPasswordArgument()
    {
        if (!File.Exists(Path.Combine(_fixture.StoragePath, SampleDocuments.ProtectedDocumentDocx)))
        {
            _output.WriteLine($"Sample '{SampleDocuments.ProtectedDocumentDocx}' not present — skipping.");
            return;
        }

        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.AddWatermark.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.ProtectedDocumentDocx },
                ["text"] = "AUTHORIZED COPY",
                ["password"] = SampleDocuments.ProtectedDocumentPassword,
            });

        if (response.IsError == true)
            throw new InvalidOperationException(
                $"Protected-document watermarking failed: {ToolResponse.Text(response)}");

        var body = ToolResponse.Text(response);
        _output.WriteLine(body);

        Assert.DoesNotContain("Watermarking failed for", body);

        var watermarkedPath = Path.Combine(_fixture.StoragePath, "protected-document_watermarked.docx");
        Assert.True(File.Exists(watermarkedPath),
            $"Expected watermarked file at '{watermarkedPath}'. Response body:\n{body}");
    }
}
