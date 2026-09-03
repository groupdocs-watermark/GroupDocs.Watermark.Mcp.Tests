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

    /// Points the suite at a locally built server DLL instead of the published NuGet.
    /// Set it to run these tests before a release is published - same tests, same protocol,
    /// against the build on disk.
    public const string LocalServerVariable = "MCP_SERVER_DLL";

    /// Which channel the server came from - "local" or "dnx". Named in failures so a red
    /// test never leaves you guessing which artifact was exercised.
    public string Channel { get; private set; } = "dnx";

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

        var launch = ResolveLaunch();

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "groupdocs-watermark-mcp",
            Command = launch.Command,
            Arguments = launch.Arguments,
            WorkingDirectory = StoragePath,
            EnvironmentVariables = BuildServerEnv(),
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        Client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);
    }

    private (string Command, string[] Arguments) ResolveLaunch()
    {
        var localDll = Environment.GetEnvironmentVariable(LocalServerVariable);
        if (!string.IsNullOrWhiteSpace(localDll))
        {
            if (!File.Exists(localDll))
                throw new InvalidOperationException(
                    $"{LocalServerVariable} is set to '{localDll}', but no such file exists. Build the " +
                    "server project first, or unset the variable to test the published package.");

            Channel = "local";
            return (CommandResolver.Resolve("dotnet"), [localDll]);
        }

        Channel = "dnx";
        var packageSpec = PackageVersion.IsLatest
            ? "GroupDocs.Watermark.Mcp"
            : $"GroupDocs.Watermark.Mcp@{PackageVersion.Value}";
        return (CommandResolver.Resolve("dnx"), [packageSpec, "--yes"]);
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

        // Forwarded only when BOTH are present. One alone exercises the server's
        // half-configured fallback, not metered licensing - and the server warns about it,
        // so a partially-configured CI secret would otherwise surface as a confusing pass.
        var meteredPublic = Environment.GetEnvironmentVariable(MeteredKeys.PublicKeyVariable);
        var meteredPrivate = Environment.GetEnvironmentVariable(MeteredKeys.PrivateKeyVariable);
        if (!string.IsNullOrWhiteSpace(meteredPublic) && !string.IsNullOrWhiteSpace(meteredPrivate))
        {
            env[MeteredKeys.PublicKeyVariable] = meteredPublic;
            env[MeteredKeys.PrivateKeyVariable] = meteredPrivate;
        }

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
