using System.Collections.Concurrent;
using Nethereum.RPC.Accounts;

namespace Nethereum.X402.Blockchain;

internal sealed class FacilitatorAccounts
{
    private readonly IAccount _account;
    private readonly ConcurrentDictionary<int, IAccount> _byChainId = new();

    public FacilitatorAccounts(IAccount account)
    {
        _account = account;
    }

    public IAccount For(int chainId) =>
        _account is Nethereum.Web3.Accounts.Account account
            ? _byChainId.GetOrAdd(chainId, id => new Nethereum.Web3.Accounts.Account(account.PrivateKey, id))
            : _account;
}
