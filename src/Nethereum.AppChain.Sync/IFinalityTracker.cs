using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Sync
{
    public interface IFinalityTracker
    {
        BigInteger LastFinalizedBlock { get; }
        BigInteger LastSoftBlock { get; }

        Task<bool> IsFinalizedAsync(BigInteger blockNumber);
        Task<bool> IsSoftAsync(BigInteger blockNumber);

        Task MarkAsFinalizedAsync(BigInteger blockNumber);
        Task MarkAsSoftAsync(BigInteger blockNumber);
        Task MarkRangeAsFinalizedAsync(BigInteger fromBlock, BigInteger toBlock);

        Task<BigInteger> GetLatestFinalizedBlockAsync();
        Task<BigInteger> GetLatestSoftBlockAsync();
    }

    public enum BlockFinality
    {
        Unknown,
        Soft,
        Finalized
    }
}
