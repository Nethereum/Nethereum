using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    /// <summary>
    /// Persists block uncles (ommers) keyed by block hash. Uncles are needed
    /// to compute correct miner reward credits during block re-execution —
    /// the eth/68 wire format delivers uncles inside the block body, but they
    /// are not part of the canonical block header. Without this store, an
    /// EVM-fix revalidation cycle ("reset state + re-execute from genesis")
    /// would have to re-fetch every block body from a peer to recover the
    /// uncles. With this store, re-execution is a pure-local read loop.
    ///
    /// Together with <see cref="IBlockStore"/> (headers) +
    /// <see cref="ITransactionStore"/> (transactions) this is the complete
    /// chain-data persistence: everything the <see cref="BlockExecutor"/>
    /// needs to re-execute any historic block from local storage alone.
    /// </summary>
    public interface IUncleStore
    {
        Task SaveAsync(byte[] blockHash, IList<BlockHeader> uncles);
        Task<IList<BlockHeader>> GetByBlockHashAsync(byte[] blockHash);
        Task<IList<BlockHeader>> GetByBlockNumberAsync(BigInteger blockNumber);
        Task DeleteByBlockHashAsync(byte[] blockHash);
        Task DeleteByBlockNumberAsync(BigInteger blockNumber);
    }
}
