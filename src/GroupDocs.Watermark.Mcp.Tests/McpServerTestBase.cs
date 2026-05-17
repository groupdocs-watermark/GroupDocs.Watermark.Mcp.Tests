using GroupDocs.Watermark.Mcp.IntegrationTests.Fixtures;
using Xunit;

namespace GroupDocs.Watermark.Mcp.IntegrationTests;

/// Base class for every integration test class. xUnit instantiates a test
/// class once PER TEST METHOD, so each test method gets its own fresh MCP
/// server process via the IAsyncLifetime hooks below.
///
/// Why per-test and NOT a shared `ICollectionFixture` server:
/// GroupDocs.Watermark's evaluation mode caps document loads at 10 per
/// process ("Only 10 documents can be loaded per application run in
/// evaluation mode"). A single server process shared across the whole suite
/// exhausts that budget partway through — the integration suite opens ~30
/// documents total. A fresh process per test resets the counter; no single
/// test opens more than ~3 documents, so the cap is never reached. The suite
/// runs fully UNLICENSED — see Pitfall #20 in the clone-to-new-product.md
/// prompt for the full rationale.
public abstract class McpServerTestBase : IAsyncLifetime
{
    protected McpServerFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = new McpServerFixture();
        await _fixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        if (_fixture is not null)
            await _fixture.DisposeAsync();
    }
}
