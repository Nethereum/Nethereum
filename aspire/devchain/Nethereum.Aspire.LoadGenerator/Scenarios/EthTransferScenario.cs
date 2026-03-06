using Nethereum.Aspire.LoadGenerator.Services;

namespace Nethereum.Aspire.LoadGenerator.Scenarios;

public class EthTransferScenario : ILoadScenario
{
    private AccountManager _accountManager = null!;

    public string Name => "eth-transfer";

    public Task InitializeAsync(AccountManager accountManager, string rpcUrl, ILogger logger)
    {
        _accountManager = accountManager;
        logger.LogInformation("EthTransferScenario initialized");
        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(int workerIndex)
    {
        var (account, web3) = _accountManager.GetAccountForWorker(workerIndex);
        var toAddress = _accountManager.GetRandomAddress();

        await web3.Eth.GetEtherTransferService()
            .TransferEtherAndWaitForReceiptAsync(toAddress, 0.001m);
    }
}
