using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.Storage.Entities;
using Nethereum.Hex.HexTypes;

namespace Nethereum.BlockchainProcessing.Storage.Repositories
{
    public interface IBlockRepository
    {
        Task UpsertBlockAsync(Nethereum.RPC.Eth.DTOs.Block source);
        Task<IBlockView> FindByBlockNumberAsync(HexBigInteger blockNumber);
    }
}