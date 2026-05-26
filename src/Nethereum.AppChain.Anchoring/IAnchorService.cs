using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Anchoring
{
    public interface IAnchorService
    {
        Task<AnchorInfo> AnchorBlockAsync(
            BigInteger blockNumber,
            byte[] stateRoot,
            byte[] transactionsRoot,
            byte[] receiptsRoot);

        Task<AnchorInfo> AnchorBlockAsync(
            BigInteger blockNumber,
            byte[] stateRoot,
            byte[] transactionsRoot,
            byte[] receiptsRoot,
            byte[] blockHash,
            AnchorSubmissionPayload submission)
            => AnchorBlockAsync(blockNumber, stateRoot, transactionsRoot, receiptsRoot);

        Task InitializeAsync() => Task.CompletedTask;

        Task<AnchorInfo?> GetAnchorAsync(BigInteger blockNumber);

        Task<BigInteger> GetLatestAnchoredBlockAsync();

        Task<bool> VerifyAnchorAsync(
            BigInteger blockNumber,
            byte[] stateRoot,
            byte[] transactionsRoot,
            byte[] receiptsRoot);
    }
}
