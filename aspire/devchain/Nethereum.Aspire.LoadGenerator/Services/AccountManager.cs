using Nethereum.Signer;
using Nethereum.Web3.Accounts;

namespace Nethereum.Aspire.LoadGenerator.Services;

public class AccountManager
{
    private readonly List<Account> _accounts = new();
    private readonly List<Nethereum.Web3.Web3> _web3Instances = new();
    private readonly int _chainId;

    public IReadOnlyList<Account> Accounts => _accounts;
    public IReadOnlyList<Nethereum.Web3.Web3> Web3Instances => _web3Instances;

    public AccountManager(int chainId)
    {
        _chainId = chainId;
    }

    public async Task InitializeAsync(string rpcUrl, string masterPrivateKey, int accountCount, ILogger logger)
    {
        var masterAccount = new Account(masterPrivateKey, _chainId);
        masterAccount.TransactionManager.UseLegacyAsDefault = false;
        var masterWeb3 = new Nethereum.Web3.Web3(masterAccount, rpcUrl);

        for (int i = 0; i < accountCount; i++)
        {
            var key = EthECKey.GenerateKey();
            var account = new Account(key, _chainId);
            account.TransactionManager.UseLegacyAsDefault = false;
            _accounts.Add(account);

            var web3 = new Nethereum.Web3.Web3(account, rpcUrl);
            web3.TransactionReceiptPolling.SetPollingRetryIntervalInMilliseconds(100);
            _web3Instances.Add(web3);
        }

        logger.LogInformation("Funding {Count} test accounts from master {Master}...",
            accountCount, masterAccount.Address);

        foreach (var account in _accounts)
        {
            await masterWeb3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(account.Address, 100m);
        }

        logger.LogInformation("All {Count} accounts funded", accountCount);
    }

    public (Account Account, Nethereum.Web3.Web3 Web3) GetAccountForWorker(int workerIndex)
    {
        var idx = workerIndex % _accounts.Count;
        return (_accounts[idx], _web3Instances[idx]);
    }

    public string GetRandomAddress()
    {
        return _accounts[Random.Shared.Next(_accounts.Count)].Address;
    }

    public async Task ResetNonceAsync(int workerIndex)
    {
        var idx = workerIndex % _accounts.Count;
        var account = _accounts[idx];
        if (account.NonceService is Nethereum.RPC.NonceServices.InMemoryNonceService inMemoryNonce)
        {
            await inMemoryNonce.ResetNonceAsync();
        }
    }
}
