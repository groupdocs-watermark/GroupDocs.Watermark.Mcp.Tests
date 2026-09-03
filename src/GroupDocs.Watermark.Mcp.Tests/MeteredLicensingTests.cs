using GroupDocs.Watermark.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Watermark.Mcp.IntegrationTests;

/// <summary>
/// Proves metered licensing actually engages on the real artifact.
/// </summary>
/// <remarks>
/// <para><b>These tests spend real money.</b> Every billable operation against a metered key
/// consumes credit, so they are excluded from the default run by their Metered category and
/// triggered deliberately - weekly or on demand. Use a dedicated CI metered account with a
/// capped balance, never the production key.</para>
///
/// <para>They also do not skip when unconfigured. The audit found licence-dependent tests
/// silently no-opping and counting as Passed; the gating lives in the CI job, where it is
/// visible, so here a missing key is a hard failure.</para>
/// </remarks>
[Trait("Category", "Metered")]
public class MeteredLicensingTests : McpServerTestBase
{
    private readonly ITestOutputHelper _output;

    public MeteredLicensingTests(ITestOutputHelper output)
    {
        MeteredKeys.RequireConfigured();
        _output = output;
    }

    private const string LicenseStatusTool = "get_license_status";

    // ---- Tier 1: does metered licensing engage at all? ---------------------
    // The cheapest possible proof and the most important: it opens no document, yet
    // exercises the whole chain - env var -> SetMeteredKeyCore -> the engine accepted the
    // pair -> GetConsumptionQuantity returned a real reading.

    [Fact]
    public async Task GetLicenseStatus_ReportsMeteredMode()
    {
        var response = await _fixture.Client.CallToolAsync(
            LicenseStatusTool, new Dictionary<string, object?>());

        Assert.False(response.IsError ?? false,
            $"[{_fixture.Channel}] {LicenseStatusTool} reported an error: {ToolResponse.Text(response)}");

        var json = ToolResponse.Json(response);
        _output.WriteLine(json.ToString());

        Assert.Equal("metered", json.GetProperty("mode").GetString());
        Assert.True(json.GetProperty("licensed").GetBoolean(),
            "Metered is a licensed state, so licensed must be true.");

        Assert.True(json.TryGetProperty("consumption", out var consumption),
            "Metered mode must report a consumption block.");

        // An error here means the engine could not read usage - most often no outbound
        // connectivity, since metered reports back to GroupDocs servers.
        Assert.False(consumption.TryGetProperty("error", out _),
            $"Consumption reading failed: {consumption}");

        Assert.True(consumption.TryGetProperty("quantity", out var quantity)
                    && quantity.ValueKind == System.Text.Json.JsonValueKind.Number,
            "Expected a numeric consumption quantity.");
        _output.WriteLine($"quantity={quantity.GetDecimal()}");
    }

    // ---- Consumption invariant ---------------------------------------------

    [Fact]
    public async Task Consumption_NeverGoesBackwards()
    {
        var before = await ReadQuantityAsync();

        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);
        await _fixture.Client.CallToolAsync(
            catalog.GetDocumentInfo.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.SyntheticPdf },
            });

        var after = await ReadQuantityAsync();
        _output.WriteLine($"consumption before={before} after={after} delta={after - before}");

        // Only the invariant is asserted, NOT an increase. Measured 2026-09-01: the info
        // tools do not bill - a plain info call moves the counter by exactly 0.0 - so
        // asserting an increase off this call would be permanently red.
        //
        // TODO: add a product-specific test that performs real billable work and asserts
        // after > before. GroupDocs.Comparison.Mcp.Tests has the reference version, where
        // analyze_changes moves the counter immediately.
        Assert.True(after >= before,
            $"Consumption went backwards ({before} -> {after}), which should never happen.");
    }

    // ---- Secret hygiene ----------------------------------------------------

    [Fact]
    public async Task LicenseStatus_NeverEchoesTheKeys()
    {
        var response = await _fixture.Client.CallToolAsync(
            LicenseStatusTool, new Dictionary<string, object?>());

        var text = ToolResponse.Text(response);

        // GitHub masks registered secrets in logs, but that is a backstop against
        // disclosure, not a guarantee our own code redacts. Assert the redaction.
        Assert.DoesNotContain(MeteredKeys.PrivateKey!, text, StringComparison.Ordinal);
        Assert.DoesNotContain(MeteredKeys.PublicKey!, text, StringComparison.Ordinal);
    }

    private async Task<decimal> ReadQuantityAsync()
    {
        var response = await _fixture.Client.CallToolAsync(
            LicenseStatusTool, new Dictionary<string, object?>());
        var consumption = ToolResponse.Json(response).GetProperty("consumption");
        return consumption.TryGetProperty("quantity", out var quantity)
               && quantity.ValueKind == System.Text.Json.JsonValueKind.Number
            ? quantity.GetDecimal()
            : 0m;
    }
}
