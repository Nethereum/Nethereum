using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.PrivacyPools
{
    public interface IPrivacyPoolProofProvider
    {
        Task<RagequitProofResult> GenerateRagequitProofAsync(RagequitWitnessInput input, CancellationToken cancellationToken = default);
        Task<WithdrawalProofResult> GenerateWithdrawalProofAsync(WithdrawalWitnessInput input, CancellationToken cancellationToken = default);
    }
}
