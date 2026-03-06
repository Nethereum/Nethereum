using Nethereum.Aspire.LoadGenerator.Contracts;
using Nethereum.Aspire.LoadGenerator.Services;
using System.Numerics;

namespace Nethereum.Aspire.LoadGenerator.Scenarios;

public class Erc20Scenario : ILoadScenario
{
    private AccountManager _accountManager = null!;
    private string _tokenAddress = "";

    public string Name => "erc20-transfer";

    public async Task InitializeAsync(AccountManager accountManager, string rpcUrl, ILogger logger)
    {
        _accountManager = accountManager;

        var (_, web3) = accountManager.GetAccountForWorker(0);

        logger.LogInformation("Deploying ERC-20 token contract...");
        var gas = await web3.Eth.DeployContract.EstimateGasAsync(
            Erc20Contract.ABI,
            Erc20Contract.BYTECODE,
            web3.TransactionManager.Account.Address);
        var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
            Erc20Contract.ABI,
            Erc20Contract.BYTECODE,
            web3.TransactionManager.Account.Address,
            gas);

        _tokenAddress = receipt.ContractAddress;
        logger.LogInformation("ERC-20 deployed at {Address}", _tokenAddress);

        var mintAmount = BigInteger.Parse("1000000000000000000000000");
        foreach (var account in accountManager.Accounts)
        {
            var mintHandler = web3.Eth.GetContractTransactionHandler<Erc20Contract.MintFunction>();
            await mintHandler.SendRequestAndWaitForReceiptAsync(_tokenAddress,
                new Erc20Contract.MintFunction { To = account.Address, Amount = mintAmount });
        }

        logger.LogInformation("Minted tokens to all {Count} accounts", accountManager.Accounts.Count);
    }

    public async Task ExecuteAsync(int workerIndex)
    {
        var (_, web3) = _accountManager.GetAccountForWorker(workerIndex);
        var toAddress = _accountManager.GetRandomAddress();

        var transferHandler = web3.Eth.GetContractTransactionHandler<Erc20Contract.TransferFunction>();
        await transferHandler.SendRequestAndWaitForReceiptAsync(_tokenAddress,
            new Erc20Contract.TransferFunction
            {
                To = toAddress,
                Value = BigInteger.One
            });
    }
}
