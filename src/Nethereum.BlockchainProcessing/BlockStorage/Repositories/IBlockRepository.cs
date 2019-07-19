using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.Hex.HexTypes;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface IBlockRepository
    {
        Task UpsertBlockAsync(Nethereum.RPC.Eth.DTOs.Block source);
        Task<IBlockView> FindByBlockNumberAsync(HexBigInteger blockNumber);
    }
}