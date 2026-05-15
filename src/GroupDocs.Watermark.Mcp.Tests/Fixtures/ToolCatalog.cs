using ModelContextProtocol.Client;

namespace GroupDocs.Watermark.Mcp.IntegrationTests.Fixtures;

/// Resolves tool names by keyword. The server-side attribute [McpServerTool] uses
/// the method name converted to snake_case (AddWatermark → add_watermark,
/// GetDocumentInfo → get_document_info), so keywords must be snake_case substrings
/// of the wire name (NOT the PascalCase identifier — see Pitfall #15 in the clone
/// prompt). Keywords picked here are mutually exclusive: `add_watermark` matches
/// AddWatermark only (not `add_image_watermark`), `image` matches AddImageWatermark
/// only, etc.
internal sealed class ToolCatalog
{
    private readonly IReadOnlyList<McpClientTool> _tools;

    private ToolCatalog(IReadOnlyList<McpClientTool> tools) => _tools = tools;

    public static async Task<ToolCatalog> LoadAsync(McpClient client, CancellationToken ct = default)
    {
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        return new ToolCatalog(tools.ToList());
    }

    public IReadOnlyList<McpClientTool> All => _tools;

    public McpClientTool AddWatermark      => Resolve("add_watermark");
    public McpClientTool AddImageWatermark => Resolve("image");
    public McpClientTool SearchWatermarks  => Resolve("search");
    public McpClientTool RemoveWatermarks  => Resolve("remove");
    public McpClientTool GetDocumentInfo   => Resolve("document_info");

    private McpClientTool Resolve(string keyword) =>
        _tools.FirstOrDefault(t => t.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No tool with name containing '{keyword}'. Found: {string.Join(", ", _tools.Select(t => t.Name))}");
}
