using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

        public Task<IReadOnlyList<BigInteger>> GetUnprovenBlockNumbersAsync()
        {
            var unproven = _witnesses.Keys
                .Where(k => !_proofs.ContainsKey(k))
                .OrderBy(k => k)
                .ToList();
            return Task.FromResult<IReadOnlyList<BigInteger>>(unproven);
        }

        public Task PurgeWitnessesAsync(WitnessRetentionPolicy policy, BigInteger currentBlock)
        {
            if (policy == null || policy.Mode == WitnessRetentionMode.Forever)
                return Task.CompletedTask;

            var toRemove = new List<BigInteger>();

            foreach (var blockNumber in _witnesses.Keys)
            {
                bool shouldPurge = false;

                switch (policy.Mode)
                {
                    case WitnessRetentionMode.UntilProven:
                        shouldPurge = _proofs.ContainsKey(blockNumber);
                        break;
                    case WitnessRetentionMode.Blocks:
                        shouldPurge = currentBlock - blockNumber >= policy.Value;
                        break;
                }

                if (shouldPurge)
                    toRemove.Add(blockNumber);
            }

            foreach (var bn in toRemove)
                _witnesses.TryRemove(bn, out _);

            return Task.CompletedTask;
        }
    }
}
