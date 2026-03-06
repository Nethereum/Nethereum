using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public interface ITokenMetadataRepository
    {
        Task UpsertAsync(TokenMetadata metadata);
        Task<ITokenMetadataView> GetByContractAsync(string contractAddress);
    }
}
