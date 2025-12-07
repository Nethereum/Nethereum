using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.RPC.Chain;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Services.Transactions;
using Nethereum.Wallet.Services;

namespace Nethereum.Wallet.Storage
{
    public interface IWalletStorageService
    {
        Task<List<ChainFeature>> GetUserNetworksAsync();
        Task SaveUserNetworksAsync(List<ChainFeature> networks);
        Task SaveUserNetworkAsync(ChainFeature network);
        Task DeleteUserNetworkAsync(BigInteger chainId);
        Task<bool> UserNetworksExistAsync();
        Task ClearUserNetworksAsync();
        
        Task SetRpcHealthCacheAsync(string rpcUrl, RpcEndpointHealthCache healthInfo);
        Task<RpcEndpointHealthCache?> GetRpcHealthCacheAsync(string rpcUrl);
        
        Task<List<string>> GetActiveRpcsAsync(BigInteger chainId);
        Task SetActiveRpcsAsync(BigInteger chainId, List<string> rpcUrls);
        Task RemoveRpcAsync(BigInteger chainId, string rpcUrl);
        
        Task<List<string>> GetCustomRpcsAsync(BigInteger chainId);
        Task SaveCustomRpcAsync(BigInteger chainId, string rpcUrl);
        Task RemoveCustomRpcAsync(BigInteger chainId, string rpcUrl);
        
        Task SetSelectedNetworkAsync(long chainId);
        Task<long?> GetSelectedNetworkAsync();
        
        Task SetSelectedAccountAsync(string accountAddress);
        Task<string?> GetSelectedAccountAsync();
        
        Task<RpcSelectionConfiguration?> GetRpcSelectionConfigAsync(BigInteger chainId);
        Task SaveRpcSelectionConfigAsync(RpcSelectionConfiguration config);
        
        Task<List<TransactionInfo>> GetPendingTransactionsAsync(BigInteger chainId);
        Task<List<TransactionInfo>> GetRecentTransactionsAsync(BigInteger chainId);
        Task SaveTransactionAsync(BigInteger chainId, TransactionInfo transaction);
        Task UpdateTransactionStatusAsync(BigInteger chainId, string hash, TransactionStatus status);
        Task<TransactionInfo?> GetTransactionByHashAsync(BigInteger chainId, string hash);
        Task DeleteTransactionAsync(BigInteger chainId, string hash);
        Task ClearTransactionsAsync(BigInteger chainId);

        Task<List<Nethereum.Wallet.Services.DappPermission>> GetDappPermissionsAsync(string? accountAddress = null);
        Task AddDappPermissionAsync(string accountAddress, string origin);
        Task RemoveDappPermissionAsync(string accountAddress, string origin);

        Task SaveNetworkPreferenceAsync(string key, bool value);
        Task<bool?> GetNetworkPreferenceAsync(string key);
    }
    public class RpcEndpointHealthCache
    {
        public string RpcUrl { get; set; } = "";
        public bool IsHealthy { get; set; }
        public double ResponseTime { get; set; }
        public DateTime LastChecked { get; set; }
    }
}
