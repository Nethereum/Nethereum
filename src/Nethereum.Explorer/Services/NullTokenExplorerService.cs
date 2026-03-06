using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.Explorer.Services;

public class NullTokenExplorerService : ITokenExplorerService
{
    public bool IsAvailable => false;

    public Task<List<ITokenBalanceView>> GetTokenBalancesAsync(string address)
        => Task.FromResult(new List<ITokenBalanceView>());

    public Task<List<INFTInventoryView>> GetNFTInventoryAsync(string address)
        => Task.FromResult(new List<INFTInventoryView>());

    public Task<List<ITokenTransferLogView>> GetTransfersByAddressAsync(string address, int page, int pageSize)
        => Task.FromResult(new List<ITokenTransferLogView>());

    public Task<List<ITokenTransferLogView>> GetTransfersByContractAsync(string contractAddress, int page, int pageSize)
        => Task.FromResult(new List<ITokenTransferLogView>());

    public Task<ITokenMetadataView?> GetTokenMetadataAsync(string contractAddress)
        => Task.FromResult<ITokenMetadataView?>(null);
}
