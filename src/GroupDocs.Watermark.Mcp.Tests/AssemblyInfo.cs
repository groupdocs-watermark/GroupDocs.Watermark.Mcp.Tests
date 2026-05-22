using Xunit;

// Run the whole suite serially. Each test method spawns its OWN dnx-launched MCP
// server process (see McpServerTestBase — required to dodge the eval-mode 10-doc
// cap). With xUnit's default cross-collection parallelism, many dnx restore+launch
// processes start at once; under runner contention a child can die before the MCP
// initialize handshake completes, surfacing as
// "System.IO.IOException: The server shut down unexpectedly." in
// McpServerFixture.InitializeAsync (worst on the slower Ubuntu hosted runner).
// Disabling parallelization ensures only one server boots at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
