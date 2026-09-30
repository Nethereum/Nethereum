using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Storage
{
    /// <summary>
    /// Persists the EIP-7928 block access list keyed by block hash. This interface is the ONE place the
    /// retention rule is written down; every other file that participates points here.
    ///
    /// <para>EIP-7928: <i>"The <c>BlockAccessList</c> is not included in the block
    /// body. The EL stores BALs separately and transmits them as a field in the
    /// <c>ExecutionPayload</c> via the engine API."</i> This is that separate store —
    /// the list is built during execution and committed to only as
    /// <c>header.block_access_list_hash</c>, so without it every block's list is
    /// discarded the moment its hash has been checked.</para>
    ///
    /// <para>EIP-7928: <i>"The EL MUST retain BALs for at least the duration of the
    /// weak subjectivity period (<c>=3533 epochs</c>) to support synchronization with
    /// re-execution after being offline for less than the WSP."</i></para>
    ///
    /// <para>The value is the RLP encoding produced by
    /// <see cref="Nethereum.Model.BlockAccessListRLPEncoder"/> — the same bytes the
    /// header's hash commits to, so <c>keccak256</c> of what is read back reproduces
    /// <c>header.BlockAccessListHash</c>. EIP-7928: <i>"The <c>blockAccessList</c>
    /// field contains the RLP-encoded BAL or <c>null</c> for pre-Amsterdam blocks or
    /// when data has been pruned."</i> Absence is therefore <c>null</c>, and a block
    /// that changed nothing is the one-byte empty RLP list <c>0xc0</c> — the two are
    /// distinguishable without a convention, unlike <see cref="IWithdrawalStore"/>
    /// where an empty list has to be written as empty rather than null.</para>
    ///
    /// <para>Every implementation returns a buffer the caller may keep and mutate without
    /// reaching back into the store, and retains a snapshot of what it was handed. A store
    /// whose contract is byte-exactness cannot share a mutable array with its caller.</para>
    ///
    /// <para>Mirrors <see cref="IUncleStore"/> and <see cref="IWithdrawalStore"/>'s
    /// lifecycle exactly: write at body-persist time, read at block-stream time, and delete
    /// by hash OR by number so <see cref="Services.RewindCoordinator"/>'s orphaned-tail walk
    /// can reclaim a reorged block's list alongside its logs, receipts, transactions, uncles
    /// and withdrawals. Deleting is per-block and idempotent — a number that is already gone
    /// is not an error. Pruning at the retention horizon is a separate policy and is not this
    /// surface.</para>
    /// </summary>
    public interface IBlockAccessListStore
    {
        Task SaveAsync(byte[] blockHash, byte[] blockAccessListRlp);
        Task<byte[]> GetByBlockHashAsync(byte[] blockHash);
        Task<byte[]> GetByBlockNumberAsync(BigInteger blockNumber);
        Task DeleteByBlockHashAsync(byte[] blockHash);
        Task DeleteByBlockNumberAsync(BigInteger blockNumber);
    }
}
