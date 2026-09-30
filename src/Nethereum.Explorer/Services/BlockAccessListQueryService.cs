using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Explorer.Services;

/// <summary>
/// EIP-7928: <i>"The <c>BlockAccessList</c> is not included in the block body. The EL
/// stores BALs separately"</i> — so it is not in the indexed block row and has to be
/// asked of a node, unless an indexer has already stored it.
/// </summary>
public class BlockAccessListQueryService : IBlockAccessListQueryService
{
    private readonly ExplorerWeb3Factory _web3Factory;
    private readonly IBlockAccessListRepository _blockAccessListRepository;

    public BlockAccessListQueryService(
        ExplorerWeb3Factory web3Factory,
        IBlockAccessListRepository blockAccessListRepository = null)
    {
        _web3Factory = web3Factory;
        _blockAccessListRepository = blockAccessListRepository;
    }

    public async Task<List<AccountAccess>?> GetForBlockAsync(long blockNumber)
    {
        var fromRepository = await GetFromRepositoryAsync(blockNumber);
        if (fromRepository != null && fromRepository.Count > 0) return fromRepository;

        return await GetFromLiveNodeAsync(blockNumber);
    }

    private async Task<List<AccountAccess>?> GetFromRepositoryAsync(long blockNumber)
    {
        if (_blockAccessListRepository == null) return null;

        try
        {
            var records = await _blockAccessListRepository.GetForBlockAsync(blockNumber);
            return records?.ToList();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<List<AccountAccess>?> GetFromLiveNodeAsync(long blockNumber)
    {
        var web3 = _web3Factory.GetWeb3();
        if (web3 == null) return null;

        try
        {
            return await web3.Eth.Blocks.GetBlockAccessList
                .SendRequestAsync(new BlockParameter(new HexBigInteger(blockNumber)));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
