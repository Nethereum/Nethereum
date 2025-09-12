using System.Collections.Generic;

namespace Nethereum.Wallet.UI.Components.WalletAccounts
{
    public interface IAccountTypeMetadataRegistry
    {
        IAccountTypeMetadata? GetMetadata(string accountType);
        IEnumerable<IAccountTypeMetadata> GetAllMetadata();
        IEnumerable<IAccountTypeMetadata> GetVisibleMetadata();
        bool HasMetadata(string accountType);
    }
}