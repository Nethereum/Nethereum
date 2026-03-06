using Nethereum.Aspire.LoadGenerator.Services;
using Nethereum.Web3;

namespace Nethereum.Aspire.LoadGenerator.Scenarios;

public interface ILoadScenario
{
    string Name { get; }
    Task InitializeAsync(AccountManager accountManager, string rpcUrl, ILogger logger);
    Task ExecuteAsync(int workerIndex);
}
