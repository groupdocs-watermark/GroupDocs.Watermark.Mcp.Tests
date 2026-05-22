using System.Text;

namespace GroupDocs.Watermark.Mcp.IntegrationTests.Fixtures;

/// Builds tiny, self-contained fixture documents on disk (so tests have a
/// known synthetic baseline that can never go missing in CI) AND copies any
/// real sample files committed under the repo's `Files/` folder into the same
/// storage directory so tests can cover real formats too.
internal static class SampleDocuments
{
    // Synthetic fixtures — generated at startup so tests have a known
    // structural baseline they can assert on regardless of what real fixtures
    // are committed to Files/. Currently a single bare 1-page PDF (no
    // watermarks) — used by the "zero watermarks" assertion in
    // SearchWatermarksTests and the smallest-possible-AddWatermark target in
    // AddWatermarkTests.
    public const string SyntheticPdf = "synthetic.pdf";

    // Real samples copied from the public GroupDocs.Watermark-for-.NET Examples
    // repo. See Files/README.md for provenance and why each was chosen.
    public const string DocumentPdf = "document.pdf";
    public const string DocumentDocx = "document.docx";
    public const string DocumentXlsx = "document.xlsx";
    public const string PresentationPptx = "presentation.pptx";
    public const string ImagePng = "image.png";
    public const string ProtectedDocumentDocx = "protected-document.docx";
    public const string DiagramVsdx = "diagram.vsdx";

    /// Real samples to copy into the server's storage folder at fixture startup.
    /// Tests reference these by the constants above; tests that need a specific
    /// format degrade gracefully if a sample isn't present (see how each test's
    /// `File.Exists(...) ? proceed : skip` guard works).
    public static IReadOnlyList<string> RealSamples { get; } = new[]
    {
        DocumentPdf, DocumentDocx, DocumentXlsx, PresentationPptx,
        ImagePng, ProtectedDocumentDocx, DiagramVsdx,
    };

    /// Password used by ProtectedDocumentDocx. Matches the upstream
    /// GroupDocs.Watermark-for-.NET fixture (LoadPasswordProtectedDocument.cs:
    /// `loadOptions.Password = "P@$$w0rd"`). Note the doubled '$'.
    public const string ProtectedDocumentPassword = "P@$$w0rd";

    public static void WriteAll(string directory)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, SyntheticPdf), BuildBarePdf());
    }

    /// Copies real sample files (those in RealSamples) from the resolved source
    /// directory into the test storage directory. Files not present in the source
    /// are skipped — the corresponding tests detect absence and skip themselves.
    public static void CopyRealSamples(string targetDirectory, string? sourceDirectory)
    {
        if (string.IsNullOrEmpty(sourceDirectory) || !Directory.Exists(sourceDirectory))
            return;

        Directory.CreateDirectory(targetDirectory);
        foreach (var name in RealSamples)
        {
            var src = Path.Combine(sourceDirectory, name);
            if (File.Exists(src))
                File.Copy(src, Path.Combine(targetDirectory, name), overwrite: true);
        }
    }

    /// Resolves the source folder containing real sample files. Order:
    ///   1. GROUPDOCS_MCP_SAMPLE_DOCS env var (set by docker-compose mount).
    ///   2. `Files/` next to the test assembly — populated by the csproj
    ///      `<None Include="..\..\Files\**\*">` copy item.
    ///   3. Walk up from the assembly to find the repo's `Files/`.
    public static string? ResolveSourceSampleDocs()
    {
        var env = Environment.GetEnvironmentVariable("GROUPDOCS_MCP_SAMPLE_DOCS");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(env))
            return env;

        var staged = Path.Combine(AppContext.BaseDirectory, "Files");
        if (Directory.Exists(staged))
            return staged;

        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = Path.Combine(dir, "Files");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    /// Minimal PDF 1.4 with one empty page and NO watermarks. Used as the
    /// synthetic baseline — tests assert SearchWatermarks returns count=0
    /// against this fixture, and AddWatermark can render onto it without
    /// any pre-existing content getting in the way.
    private static byte[] BuildBarePdf()
    {
        var body = new StringBuilder();
        var offsets = new List<int>();

        void WriteObj(string obj)
        {
            offsets.Add(body.Length);
            body.Append(obj);
        }

        body.Append("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");

        WriteObj("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        WriteObj("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        WriteObj("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> >>\nendobj\n");
        WriteObj("4 0 obj\n<< /Title (Watermark MCP synthetic baseline) /Creator (GroupDocs.Watermark.Mcp integration tests) >>\nendobj\n");

        var xrefOffset = body.Length;
        body.Append("xref\n0 5\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            body.Append($"{offset:D10} 00000 n \n");

        body.Append("trailer\n<< /Size 5 /Root 1 0 R /Info 4 0 R >>\n");
        body.Append($"startxref\n{xrefOffset}\n%%EOF");

        return Encoding.ASCII.GetBytes(body.ToString());
    }
}
