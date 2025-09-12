using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Wallet
{
    public class AccountGroup
    {
        public AccountGroup(string? groupId, IEnumerable<IWalletAccount> accounts, object? groupMetadata = null)
        {
            GroupId = groupId;
            Accounts = accounts.OrderBy(a => a.Name).ToList();
            GroupMetadata = groupMetadata;
        }
        public string? GroupId { get; }
        public IReadOnlyList<IWalletAccount> Accounts { get; }
        public object? GroupMetadata { get; }
        public int Count => Accounts.Count;
        public bool IsStandalone => string.IsNullOrEmpty(GroupId);
        public T? GetGroupMetadata<T>() where T : class => GroupMetadata as T;
    }
}