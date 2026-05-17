using ModelContextProtocol.Client;
using Xunit;

namespace GroupDocs.Watermark.Mcp.IntegrationTests.Fixtures;

/// Boots the published GroupDocs.Watermark.Mcp NuGet via `dnx` as a child process,
/// wires an MCP stdio client, and seeds a temporary storage folder with sample
/// documents.
///
/// Created once PER TEST METHOD (not shared) — see McpServerTestBase. Each test
/// gets a fresh server process so GroupDocs.Watermark's 10-document-per-process
/// evaluation-mode cap is never reached suite-wide.
public sealed class McpServerFixture : IAsyncLifetime
{
    public string StoragePath { get; } = Path.Combine(
        Path.GetTempPath(),
        $"gdwm-mcp-it-{Guid.NewGuid():N}");

    public string PackageVersionUnderTest => PackageVersion.Value;

    public McpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(StoragePath);
        SampleDocuments.WriteAll(StoragePath);
        SampleDocuments.CopyRealSamples(StoragePath, SampleDocuments.ResolveSourceSampleDocs());

        // dnx has no `@latest` literal — to get the latest stable, omit the `@<version>` entirely.
        var packageSpec = PackageVersion.IsLatest
            ? "GroupDocs.Watermark.Mcp"
            : $"GroupDocs.Watermark.Mcp@{PackageVersion.Value}";

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "groupdocs-watermark-mcp",
            Command = CommandResolver.Resolve("dnx"),
            Arguments = new[] { packageSpec, "--yes" },
            WorkingDirectory = StoragePath,
            EnvironmentVariables = BuildServerEnv(),
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        Client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);
    }

    private Dictionary<string, string?> BuildServerEnv()
    {
        var env = new Dictionary<string, string?>
        {
            ["GROUPDOCS_MCP_STORAGE_PATH"] = StoragePath,
            ["DOTNET_NOLOGO"] = "true",
        };

        // Forward license path if present — enables licensed-mode tests in CI.
        var licensePath = Environment.GetEnvironmentVariable("GROUPDOCS_LICENSE_PATH");
        if (!string.IsNullOrEmpty(licensePath))
            env["GROUPDOCS_LICENSE_PATH"] = licensePath;

        return env;
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (Client is not null)
                await Client.DisposeAsync();
        }
        catch
        {
            // Swallow disposal errors — we don't want them to mask test failures.
        }

        try
        {
            if (Directory.Exists(StoragePath))
                Directory.Delete(StoragePath, recursive: true);
        }
        catch
        {
            // Best-effort cleanup on Windows where handles may linger briefly.
        }
    }
}
