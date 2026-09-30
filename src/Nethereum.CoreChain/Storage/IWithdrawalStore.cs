using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    /// <summary>
    /// Persists EIP-4895 block withdrawals keyed by block hash. Withdrawals
    /// arrive inside the post-Shanghai block body and contribute to the
    /// header's <c>withdrawals_root</c>; without this store, re-streaming a
    /// post-Shanghai block from local storage would yield a body with
    /// <c>Withdrawals=null</c> and the executor would compute a wrong block
    /// hash, fatal-exiting on root divergence.
    ///
    /// Mirrors <see cref="IUncleStore"/>: write at body-persist time, read at
    /// block-stream time. Empty lists are written as empty (not null) so a
    /// re-execute loop can distinguish "no withdrawals in this block" from
    /// "withdrawals weren't persisted at all".
    /// </summary>
    public interface IWithdrawalStore
    {
        Task SaveAsync(byte[] blockHash, IList<Withdrawal> withdrawals);
        Task<IList<Withdrawal>> GetByBlockHashAsync(byte[] blockHash);
        Task<IList<Withdrawal>> GetByBlockNumberAsync(BigInteger blockNumber);
        Task DeleteByBlockHashAsync(byte[] blockHash);
        Task DeleteByBlockNumberAsync(BigInteger blockNumber);
    }
}
