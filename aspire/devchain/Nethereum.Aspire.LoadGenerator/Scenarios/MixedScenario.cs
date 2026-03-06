using Nethereum.Aspire.LoadGenerator.Services;

namespace Nethereum.Aspire.LoadGenerator.Scenarios;

public class MixedScenario : ILoadScenario
{
    private readonly List<(ILoadScenario Scenario, double Weight)> _scenarios = new();
    private double _totalWeight;

    public string Name => "mixed";

    public void AddScenario(ILoadScenario scenario, double weight)
    {
        _scenarios.Add((scenario, weight));
        _totalWeight += weight;
    }

    public async Task InitializeAsync(AccountManager accountManager, string rpcUrl, ILogger logger)
    {
        foreach (var (scenario, _) in _scenarios)
        {
            await scenario.InitializeAsync(accountManager, rpcUrl, logger);
        }
        logger.LogInformation("MixedScenario initialized with {Count} sub-scenarios", _scenarios.Count);
    }

    public async Task ExecuteAsync(int workerIndex)
    {
        var roll = Random.Shared.NextDouble() * _totalWeight;
        var cumulative = 0.0;

        foreach (var (scenario, weight) in _scenarios)
        {
            cumulative += weight;
            if (roll <= cumulative)
            {
                await scenario.ExecuteAsync(workerIndex);
                return;
            }
        }

        await _scenarios[^1].Scenario.ExecuteAsync(workerIndex);
    }
}
