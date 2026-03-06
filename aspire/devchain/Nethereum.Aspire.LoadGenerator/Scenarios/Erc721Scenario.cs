using Nethereum.Aspire.LoadGenerator.Contracts;
using Nethereum.Aspire.LoadGenerator.Services;
using System.Collections.Concurrent;
using System.Numerics;

namespace Nethereum.Aspire.LoadGenerator.Scenarios;

public class Erc721Scenario : ILoadScenario
{
    private AccountManager _accountManager = null!;
    private string _contractAddress = "";
    private int _nextTokenId;
    private ConcurrentDictionary<int, ConcurrentQueue<int>> _workerTokens = new();

    public string Name => "erc721-transfer";

    public async Task InitializeAsync(AccountManager accountManager, string rpcUrl, ILogger logger)
    {
        _accountManager = accountManager;

        var (_, web3) = accountManager.GetAccountForWorker(0);

        logger.LogInformation("Deploying ERC-721 token contract...");
        var deployHandler = web3.Eth.GetContractDeploymentHandler<Erc721Contract.DeploymentMessage>();
        var receipt = await deployHandler.SendRequestAndWaitForReceiptAsync(new Erc721Contract.DeploymentMessage());

        _contractAddress = receipt.ContractAddress;
        logger.LogInformation("ERC-721 deployed at {Address}", _contractAddress);

        for (int w = 0; w < accountManager.Accounts.Count; w++)
        {
            var queue = new ConcurrentQueue<int>();
            _workerTokens[w] = queue;

            var mintHandler = web3.Eth.GetContractTransactionHandler<Erc721Contract.SafeMintFunction>();
            for (int i = 0; i < 100; i++)
            {
                var tokenId = Interlocked.Increment(ref _nextTokenId);
                await mintHandler.SendRequestAndWaitForReceiptAsync(_contractAddress,
                    new Erc721Contract.SafeMintFunction
                    {
                        To = accountManager.Accounts[w].Address,
                        Uri = $"https://example.com/nft/{tokenId}"
                    });
                queue.Enqueue(tokenId);
            }
        }

        logger.LogInformation("Minted 100 NFTs to each of {Count} accounts", accountManager.Accounts.Count);
    }

    public async Task ExecuteAsync(int workerIndex)
    {
        if (!_workerTokens.TryGetValue(workerIndex, out var tokenQueue) || !tokenQueue.TryDequeue(out var tokenId))
            return;

        var (account, web3) = _accountManager.GetAccountForWorker(workerIndex);
        var targetWorker = (workerIndex + 1) % _accountManager.Accounts.Count;
        var toAddress = _accountManager.Accounts[targetWorker].Address;

        var transferHandler = web3.Eth.GetContractTransactionHandler<Erc721Contract.TransferFromFunction>();
        await transferHandler.SendRequestAndWaitForReceiptAsync(_contractAddress,
            new Erc721Contract.TransferFromFunction
            {
                From = account.Address,
                To = toAddress,
                TokenId = new BigInteger(tokenId)
            });

        _workerTokens.GetOrAdd(targetWorker, _ => new ConcurrentQueue<int>()).Enqueue(tokenId);
    }
}
