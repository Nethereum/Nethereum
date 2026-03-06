using System.Linq;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;

namespace Nethereum.Explorer.Services;

public class TokenExplorerService : ITokenExplorerService
{
    private readonly ITokenBalanceRepository _balanceRepository;
    private readonly INFTInventoryRepository _nftRepository;
    private readonly ITokenTransferLogRepository _transferLogRepository;
    private readonly ITokenMetadataRepository _metadataRepository;

    public bool IsAvailable => true;

    public TokenExplorerService(
        ITokenBalanceRepository balanceRepository,
        INFTInventoryRepository nftRepository,
        ITokenTransferLogRepository transferLogRepository,
        ITokenMetadataRepository metadataRepository)
    {
        _balanceRepository = balanceRepository;
        _nftRepository = nftRepository;
        _transferLogRepository = transferLogRepository;
        _metadataRepository = metadataRepository;
    }

    public async Task<List<ITokenBalanceView>> GetTokenBalancesAsync(string address)
    {
        var balances = await _balanceRepository.GetByAddressAsync(address);
        return balances?.ToList() ?? new List<ITokenBalanceView>();
    }

    public async Task<List<INFTInventoryView>> GetNFTInventoryAsync(string address)
    {
        var inventory = await _nftRepository.GetByAddressAsync(address);
        return inventory?.Where(n => n.Amount != "0").ToList() ?? new List<INFTInventoryView>();
    }

    public async Task<List<ITokenTransferLogView>> GetTransfersByAddressAsync(string address, int page, int pageSize)
    {
        if (page < 1) page = 1;
        pageSize = ExplorerConstants.ClampPageSize(pageSize);

        var transfers = await _transferLogRepository.GetByAddressAsync(address, page, pageSize);
        return transfers?.ToList() ?? new List<ITokenTransferLogView>();
    }

    public async Task<List<ITokenTransferLogView>> GetTransfersByContractAsync(string contractAddress, int page, int pageSize)
    {
        if (page < 1) page = 1;
        pageSize = ExplorerConstants.ClampPageSize(pageSize);

        var transfers = await _transferLogRepository.GetByContractAsync(contractAddress, page, pageSize);
        return transfers?.ToList() ?? new List<ITokenTransferLogView>();
    }

    public async Task<ITokenMetadataView?> GetTokenMetadataAsync(string contractAddress)
    {
        return await _metadataRepository.GetByContractAsync(contractAddress);
    }
}
