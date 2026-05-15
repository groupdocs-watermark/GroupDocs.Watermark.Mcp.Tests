# Files — real document fixtures for the integration suite

This folder holds the real document fixtures used by the integration suite —
seven format-specific files copied from the [GroupDocs.Watermark-for-.NET
Examples repo](https://github.com/groupdocs-watermark/GroupDocs.Watermark-for-.NET):

| File | Format | Size | Purpose |
|---|---|---|---|
| `document.pdf` | PDF | 1.1 MB | Multi-page PDF — primary AddWatermark / SearchWatermarks target |
| `document.docx` | DOCX | 37 KB | Office text — exercises Word-format watermarking |
| `document.xlsx` | XLSX | 42 KB | Spreadsheet — exercises Cells-format watermarking |
| `presentation.pptx` | PPTX | 245 KB | PowerPoint — exercises Slides-format watermarking |
| `image.png` | PNG | 1.2 MB | Image format — used both as a target document AND as the source image for `AddImageWatermark` |
| `protected-document.docx` | DOCX | 44 KB | Password-protected — exercises the `password` tool parameter |
| `diagram.vsdx` | VSDX | 32 KB | Visio diagram — exercises the System.Drawing-heavy Diagram code path |

## Wiring

The csproj's `<None Include="..\..\Files\**\*">` glob copies these to the test
output's `Files/` subfolder. The fixture's `SampleDocuments.ResolveSourceSampleDocs()`
finds that folder at runtime and seeds the server's storage path.

## Provenance

Files are copied verbatim from
`groupdocs-watermark/GroupDocs.Watermark-for-.NET/Examples/Resources/SampleFiles/Documents/`.
They are the same fixtures shipping in the public Examples repo (MIT-licensed),
so no provenance concerns. To refresh, run:

```bash
cp ../../GroupDocs.Watermark-for-.NET/Examples/Resources/SampleFiles/Documents/{document.pdf,document.docx,document.xlsx,presentation.pptx,image.png,protected-document.docx,diagram.vsdx} ./
```

## Adding new fixtures

1. Drop a binary into this folder.
2. Add a `public const string` to `SampleDocuments.cs` referencing the filename.
3. Add the constant to the `RealSamples` array if the fixture should auto-load
   into the server's storage path for every test.
4. Write a `[Theory]` entry under the relevant `*Tests.cs` referencing the new
   constant.
