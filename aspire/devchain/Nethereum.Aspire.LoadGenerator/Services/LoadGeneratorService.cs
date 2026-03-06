using System.Diagnostics;
using Nethereum.Aspire.LoadGenerator.Configuration;
using Nethereum.Aspire.LoadGenerator.Metrics;
using Nethereum.Aspire.LoadGenerator.Scenarios;
using Microsoft.Extensions.Options;

namespace Nethereum.Aspire.LoadGenerator.Services;

public class LoadGeneratorService : BackgroundService
{
    private readonly LoadGeneratorOptions _options;
    private readonly LoadGeneratorMetrics _metrics;
    private readonly ILogger<LoadGeneratorService> _logger;
    private readonly IConfiguration _configuration;

    public LoadGeneratorService(
        IOptions<LoadGeneratorOptions> options,
        LoadGeneratorMetrics metrics,
        ILogger<LoadGeneratorService> logger,
        IConfiguration configuration)
    {
        _options = options.Value;
        _metrics = metrics;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(_options.WarmupSeconds), stoppingToken);

        var rpcUrl = _configuration["services:devchain:http:0"]
            ?? _configuration.GetConnectionString("devchain")
            ?? "http://localhost:5100";

        _logger.LogInformation("Load Generator starting — scenario: {Scenario}, concurrency: {Concurrency}, rpc: {Rpc}",
            _options.ScenarioType, _options.Concurrency, rpcUrl);

        var accountManager = new AccountManager(_options.ChainId);
        await accountManager.InitializeAsync(rpcUrl, _options.PrivateKey, _options.AccountCount, _logger);

        var scenario = CreateScenario(_options.ScenarioType);
        await scenario.InitializeAsync(accountManager, rpcUrl, _logger);

        _logger.LogInformation("Scenario {Name} initialized, starting {Count} workers", scenario.Name, _options.Concurrency);

        var tasks = new List<Task>();
        for (int i = 0; i < _options.Concurrency; i++)
        {
            var workerIndex = i;
            tasks.Add(RunWorkerAsync(workerIndex, scenario, accountManager, stoppingToken));
        }

        if (_options.DurationSeconds > 0)
        {
            var durationCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            durationCts.CancelAfter(TimeSpan.FromSeconds(_options.DurationSeconds));
            try
            {
                await Task.WhenAll(tasks).WaitAsync(durationCts.Token);
            }
            catch (OperationCanceledException) { }
        }
        else
        {
            await Task.WhenAll(tasks);
        }

        _logger.LogInformation("Load Generator stopped — {Success} success, {Failed} failed, peak TPS: {TPS:F2}",
            _metrics.TotalSuccess, _metrics.TotalFailed, _metrics.PeakTps);
    }

    private async Task RunWorkerAsync(int workerIndex, ILoadScenario scenario, AccountManager accountManager, CancellationToken ct)
    {
        _metrics.WorkerStarted();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    _metrics.RecordSent();
                    await scenario.ExecuteAsync(workerIndex);
                    sw.Stop();
                    _metrics.RecordSuccess(sw.ElapsedMilliseconds);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex) when (ex.Message.Contains("nonce", StringComparison.OrdinalIgnoreCase))
                {
                    sw.Stop();
                    _metrics.RecordFailure();
                    await accountManager.ResetNonceAsync(workerIndex);
                    _logger.LogWarning("Worker {Index} nonce reset after drift", workerIndex);
                    await Task.Delay(100, ct);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    _metrics.RecordFailure();
                    _logger.LogWarning(ex, "Worker {Index} execution failed", workerIndex);
                    await Task.Delay(500, ct);
                }

                if (_options.TargetTps.HasValue)
                {
                    var targetDelayMs = 1000.0 / _options.TargetTps.Value * _options.Concurrency;
                    var actualMs = sw.ElapsedMilliseconds;
                    if (actualMs < targetDelayMs)
                    {
                        await Task.Delay((int)(targetDelayMs - actualMs), ct);
                    }
                }
            }
        }
        finally
        {
            _metrics.WorkerStopped();
        }
    }

    private static ILoadScenario CreateScenario(string scenarioType) => scenarioType.ToLowerInvariant() switch
    {
        "transfer" or "eth-transfer" => new EthTransferScenario(),
        "erc20" or "erc20-transfer" => new Erc20Scenario(),
        "erc721" or "erc721-transfer" => new Erc721Scenario(),
        "erc1155" or "erc1155-transfer" => new Erc1155Scenario(),
        "deploy" or "contract-deploy" => new ContractDeployScenario(),
        "mud" or "mud-world" => new MudWorldScenario(),
        "mixed" => CreateMixedScenario(),
        _ => new EthTransferScenario()
    };

    private static MixedScenario CreateMixedScenario()
    {
        var mixed = new MixedScenario();
        mixed.AddScenario(new EthTransferScenario(), 0.35);
        mixed.AddScenario(new Erc20Scenario(), 0.25);
        mixed.AddScenario(new Erc721Scenario(), 0.15);
        mixed.AddScenario(new Erc1155Scenario(), 0.15);
        mixed.AddScenario(new ContractDeployScenario(), 0.1);
        return mixed;
    }
}
