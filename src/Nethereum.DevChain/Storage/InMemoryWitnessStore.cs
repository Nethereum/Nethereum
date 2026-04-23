using System.Collections.Concurrent;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Proving;
using Nethereum.CoreChain.Storage;

namespace Nethereum.DevChain.Storage
{
    public class InMemoryWitnessStore : IWitnessStore
    {
        private readonly ConcurrentDictionary<BigInteger, byte[]> _witnesses = new();
        private readonly ConcurrentDictionary<BigInteger, BlockProofResult> _proofs = new();

        public Task StoreWitnessAsync(BigInteger blockNumber, byte[] witnessBytes)
        {
            _witnesses[blockNumber] = witnessBytes;
            return Task.CompletedTask;
        }

        public Task<byte[]> GetWitnessAsync(BigInteger blockNumber)
        {
            _witnesses.TryGetValue(blockNumber, out var witness);
            return Task.FromResult(witness);
        }

        public Task StoreProofAsync(BigInteger blockNumber, BlockProofResult proof)
        {
            _proofs[blockNumber] = proof;
            return Task.CompletedTask;
        }

        public Task<BlockProofResult> GetProofAsync(BigInteger blockNumber)
        {
            _proofs.TryGetValue(blockNumber, out var proof);
            return Task.FromResult(proof);
        }
    }
}
