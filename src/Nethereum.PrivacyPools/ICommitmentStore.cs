using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.PrivacyPools
{
    public interface ICommitmentStore
    {
        Task SaveAsync(PrivacyPoolCommitment commitment, int leafIndex, string poolAddress);
        Task<List<(PrivacyPoolCommitment Commitment, int LeafIndex)>> GetUnspentAsync(string poolAddress);
        Task MarkSpentAsync(BigInteger nullifierHash);
    }
}
