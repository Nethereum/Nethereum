using Nethereum.RPC.Chain;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Wallet.Services.Network
{
    public interface IChainManagementService
    {
        Task AddCustomChainAsync(ChainFeature chain);
        Task<bool> AddCustomNetworkAsync(ChainFeature customNetwork);
        Task AddCustomRpcAsync(BigInteger chainId, string rpcUrl);
        Task<ChainFeature?> AddNetworkFromChainListAsync(BigInteger chainId);
        bool CanRemoveChain(BigInteger chainId);
        Task<bool> DeleteUserNetworkAsync(BigInteger chainId);
        Task<List<ChainFeature>> GetAllChainsAsync();
        Task<string?> GetBestRpcEndpointAsync(BigInteger chainId);
        Task<ChainFeature?> GetChainAsync(BigInteger chainId);
        Task<ChainFeature?> GetCompleteChainAsync(BigInteger chainId);
        Task<List<string>> GetCustomRpcsAsync(BigInteger chainId);
        Task RefreshChainDataAsync();
        Task<bool> RefreshRpcsFromChainListAsync(BigInteger chainId);
        Task RemoveCustomChainAsync(BigInteger chainId);
        Task RemoveCustomRpcAsync(BigInteger chainId, string rpcUrl);
        Task ResetChainToDefaultAsync(BigInteger chainId);
        Task UpdateChainAsync(ChainFeature chain);
        Task UpdateChainRpcConfigurationAsync(BigInteger chainId, List<string> httpRpcs, List<string> wsRpcs);
        Task<bool> UpdateUserNetworkAsync(ChainFeature updatedNetwork);
    }
}