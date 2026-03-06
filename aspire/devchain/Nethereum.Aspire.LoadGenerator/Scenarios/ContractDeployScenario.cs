using Nethereum.Aspire.LoadGenerator.Contracts;
using Nethereum.Aspire.LoadGenerator.Services;

namespace Nethereum.Aspire.LoadGenerator.Scenarios;

public class ContractDeployScenario : ILoadScenario
{
    private AccountManager _accountManager = null!;

    public string Name => "contract-deploy";

    public Task InitializeAsync(AccountManager accountManager, string rpcUrl, ILogger logger)
    {
        _accountManager = accountManager;
        logger.LogInformation("ContractDeployScenario initialized");
        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(int workerIndex)
    {
        var (_, web3) = _accountManager.GetAccountForWorker(workerIndex);

        var gas = await web3.Eth.DeployContract.EstimateGasAsync(
            EventEmitterContract.ABI,
            EventEmitterContract.BYTECODE,
            web3.TransactionManager.Account.Address);
        await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
            EventEmitterContract.ABI,
            EventEmitterContract.BYTECODE,
            web3.TransactionManager.Account.Address,
            gas);
    }
}
