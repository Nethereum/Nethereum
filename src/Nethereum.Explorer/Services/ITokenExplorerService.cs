using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.Explorer.Services;

public interface ITokenExplorerService
{
    bool IsAvailable { get; }
    Task<List<ITokenBalanceView>> GetTokenBalancesAsync(string address);
    Task<List<INFTInventoryView>> GetNFTInventoryAsync(string address);
    Task<List<ITokenTransferLogView>> GetTransfersByAddressAsync(string address, int page, int pageSize);
    Task<List<ITokenTransferLogView>> GetTransfersByContractAsync(string contractAddress, int page, int pageSize);
    Task<ITokenMetadataView?> GetTokenMetadataAsync(string contractAddress);
}
