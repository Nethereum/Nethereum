using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public interface IUncleStore
    {
        Task SaveAsync(byte[] blockHash, IList<BlockHeader> uncles);
        Task<IList<BlockHeader>> GetByBlockHashAsync(byte[] blockHash);
        Task<IList<BlockHeader>> GetByBlockNumberAsync(BigInteger blockNumber);
        Task DeleteByBlockHashAsync(byte[] blockHash);
        Task DeleteByBlockNumberAsync(BigInteger blockNumber);
    }
}
