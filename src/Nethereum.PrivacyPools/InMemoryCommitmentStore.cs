using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.PrivacyPools
{
    public class InMemoryCommitmentStore : ICommitmentStore
    {
        private readonly List<CommitmentEntry> _entries = new();
        private readonly HashSet<BigInteger> _spentNullifiers = new();
        private readonly object _lock = new();

        public Task SaveAsync(PrivacyPoolCommitment commitment, int leafIndex, string poolAddress)
        {
            lock (_lock)
            {
                _entries.Add(new CommitmentEntry
                {
                    Commitment = commitment,
                    LeafIndex = leafIndex,
                    PoolAddress = poolAddress
                });
            }
            return Task.CompletedTask;
        }

        public Task<List<(PrivacyPoolCommitment Commitment, int LeafIndex)>> GetUnspentAsync(string poolAddress)
        {
            lock (_lock)
            {
                var result = _entries
                    .Where(e => e.PoolAddress == poolAddress && !_spentNullifiers.Contains(e.Commitment.NullifierHash))
                    .Select(e => (e.Commitment, e.LeafIndex))
                    .ToList();
                return Task.FromResult(result);
            }
        }

        public Task MarkSpentAsync(BigInteger nullifierHash)
        {
            lock (_lock)
            {
                _spentNullifiers.Add(nullifierHash);
            }
            return Task.CompletedTask;
        }

        private class CommitmentEntry
        {
            public PrivacyPoolCommitment Commitment { get; set; } = null!;
            public int LeafIndex { get; set; }
            public string PoolAddress { get; set; } = "";
        }
    }
}
