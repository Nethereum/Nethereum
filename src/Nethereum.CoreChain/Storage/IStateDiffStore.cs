using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public interface IStateDiffStore
    {
        /// <summary>
        /// Persist the reverse-diff entries for one block. The implementation
        /// stages all puts into a single backend batch internally so a kill
        /// mid-write either persists the entire diff or none of it.
        /// </summary>
        Task SaveBlockDiffAsync(BlockStateDiff diff);

        Task<(bool Found, Account PreValue)> GetFirstAccountPreValueAfterBlockAsync(string address, BigInteger blockNumber);

        Task<(bool Found, byte[] PreValue)> GetFirstStoragePreValueAfterBlockAsync(string address, BigInteger slot, BigInteger blockNumber);

        /// <summary>
        /// Read back the full reverse-diff recorded for a single block, or
        /// <c>null</c> if no diff was recorded at <paramref name="blockNumber"/>.
        /// Powers journal-based rewind: the caller iterates blocks from
        /// <c>currentBlock</c> down to <c>target+1</c>, re-applies each diff's
        /// pre-values, then deletes the diff.
        /// </summary>
        Task<BlockStateDiff> GetBlockDiffAsync(BigInteger blockNumber);

        /// <summary>
        /// Delete the reverse-diff for one block atomically. Called by the
        /// rewind path after pre-values have been restored to the state CFs.
        /// </summary>
        Task DeleteBlockDiffAsync(BigInteger blockNumber);

        Task DeleteDiffsAboveBlockAsync(BigInteger blockNumber);

        Task DeleteDiffsBelowBlockAsync(BigInteger blockNumber);

        Task<BigInteger?> GetOldestDiffBlockAsync();

        Task<BigInteger?> GetNewestDiffBlockAsync();
    }
}
