using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Nethereum.Aspire.IntegrationTests.Infrastructure;

public class AspireFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public DistributedApplication App => _app ?? throw new InvalidOperationException("App not started");
    public HttpClient DevChainClient { get; private set; } = null!;
    public HttpClient LoadGeneratorClient { get; private set; } = null!;
    public Nethereum.Web3.Web3 Web3 { get; private set; } = null!;
    public Nethereum.Web3.Accounts.Account Account { get; private set; } = null!;

    public const string PrivateKey = "5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a";
    public const string Address = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
    public const int ChainId = 31337;

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Nethereum_Aspire_AppHost>(args: ["--Testing=true"]);

        _app = await builder.BuildAsync();
        await _app.StartAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        await _app.ResourceNotifications.WaitForResourceAsync("devchain", KnownResourceStates.Running, cts.Token);
        await _app.ResourceNotifications.WaitForResourceAsync("indexer", KnownResourceStates.Running, cts.Token);
        await _app.ResourceNotifications.WaitForResourceAsync("bundler", KnownResourceStates.Running, cts.Token);
        await _app.ResourceNotifications.WaitForResourceAsync("loadgenerator", KnownResourceStates.Running, cts.Token);

        DevChainClient = _app.CreateHttpClient("devchain");
        LoadGeneratorClient = _app.CreateHttpClient("loadgenerator");
        Account = new Nethereum.Web3.Accounts.Account(PrivateKey, ChainId);
        Account.TransactionManager.UseLegacyAsDefault = false;
        Web3 = new Nethereum.Web3.Web3(Account, DevChainClient.BaseAddress!.ToString());
        Web3.TransactionReceiptPolling.SetPollingRetryIntervalInMilliseconds(100);
    }

    public async Task<NpgsqlConnection> CreateDbConnectionAsync()
    {
        var connectionString = await App.GetConnectionStringAsync("nethereumdb");
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task DisposeAsync()
    {
        LoadGeneratorClient?.Dispose();
        DevChainClient?.Dispose();

        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
