using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainProcessing.ProgressRepositories
{
    public interface IChainStateRepository
    {
        Task<ChainState> GetChainStateAsync();
        Task UpsertChainStateAsync(ChainState chainState);
    }
}
