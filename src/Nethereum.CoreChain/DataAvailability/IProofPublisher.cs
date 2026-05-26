using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.DataAvailability
{
    public class ProofBundle
    {
        public byte[] ProofBytes { get; init; }
        public byte[] ElfHash { get; init; }
        public string ProverMode { get; init; }
        public long BlockNumber { get; init; }
        public bool StateRootVerified { get; init; }
        public bool BlockHashVerified { get; init; }
        public byte[] ProverComputedStateRoot { get; init; }
        public byte[] ProverComputedBlockHash { get; init; }
    }

    public class ProofPublication
    {
        public byte[] CommitmentHash { get; init; }
        public byte[] TransactionHash { get; init; }
        public byte[] SnarkProofBytes { get; init; }
    }

    public interface IProofPublisher
    {
        Task<ProofPublication> PublishAsync(ProofBundle proof, AnchorScope scope, CancellationToken ct = default);
        Task<byte[]> RetrieveAsync(ProofPublication commitment, CancellationToken ct = default);
    }
}
