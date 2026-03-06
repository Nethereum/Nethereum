using Nethereum.Aspire.LoadGenerator.Contracts;
using Nethereum.Aspire.LoadGenerator.Services;
using System.Numerics;

namespace Nethereum.Aspire.LoadGenerator.Scenarios;

public class Erc1155Scenario : ILoadScenario
{
    private AccountManager _accountManager = null!;
    private string _contractAddress = "";

    public string Name => "erc1155-transfer";

    public async Task InitializeAsync(AccountManager accountManager, string rpcUrl, ILogger logger)
    {
        _accountManager = accountManager;

        var (_, web3) = accountManager.GetAccountForWorker(0);
        var deployerAddress = web3.TransactionManager.Account.Address;

        logger.LogInformation("Deploying ERC-1155 token contract...");
        var gas = await web3.Eth.DeployContract.EstimateGasAsync(
            Erc1155Contract.ABI,
            Erc1155Contract.BYTECODE,
            deployerAddress);
        var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
            Erc1155Contract.ABI,
            Erc1155Contract.BYTECODE,
            deployerAddress,
            gas);

        _contractAddress = receipt.ContractAddress;
        logger.LogInformation("ERC-1155 deployed at {Address}", _contractAddress);

        for (int i = 0; i < accountManager.Accounts.Count; i++)
        {
            var (_, workerWeb3) = accountManager.GetAccountForWorker(i);
            if (workerWeb3.TransactionManager.Account.Address.Equals(deployerAddress, StringComparison.OrdinalIgnoreCase))
                continue;
            var approvalHandler = workerWeb3.Eth.GetContractTransactionHandler<Erc1155Contract.SetApprovalForAllFunction>();
            await approvalHandler.SendRequestAndWaitForReceiptAsync(_contractAddress,
                new Erc1155Contract.SetApprovalForAllFunction
                {
                    Operator = deployerAddress,
                    Approved = true
                });
        }

        var mintHandler = web3.Eth.GetContractTransactionHandler<Erc1155Contract.MintFunction>();
        foreach (var account in accountManager.Accounts)
        {
            for (int tokenId = 1; tokenId <= 5; tokenId++)
            {
                await mintHandler.SendRequestAndWaitForReceiptAsync(_contractAddress,
                    new Erc1155Contract.MintFunction
                    {
                        Account = account.Address,
                        Id = new BigInteger(tokenId),
                        Amount = new BigInteger(1000),
                        Data = Array.Empty<byte>()
                    });
            }
        }

        logger.LogInformation("Minted token IDs 1-5 (1000 each) to all {Count} accounts", accountManager.Accounts.Count);
    }

    public async Task ExecuteAsync(int workerIndex)
    {
        var (account, web3) = _accountManager.GetAccountForWorker(workerIndex);
        var toAddress = _accountManager.GetRandomAddress();
        var tokenId = Random.Shared.Next(1, 6);

        var transferHandler = web3.Eth.GetContractTransactionHandler<Erc1155Contract.SafeTransferFromFunction>();
        await transferHandler.SendRequestAndWaitForReceiptAsync(_contractAddress,
            new Erc1155Contract.SafeTransferFromFunction
            {
                From = account.Address,
                To = toAddress,
                Id = new BigInteger(tokenId),
                Amount = BigInteger.One,
                Data = Array.Empty<byte>()
            });
    }
}
