using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.DataAvailability
{
    public class InlineProofContributor : IAnchorPayloadContributor
    {
        private readonly IWitnessStore _witnessStore;

        public InlineProofContributor(IWitnessStore witnessStore)
        {
            _witnessStore = witnessStore;
        }

        public AnchorPayloadSectionType Kind => AnchorPayloadSectionType.InlineProof;

        public async Task<AnchorPayloadSection> ContributeAsync(AnchorScope scope, CancellationToken ct = default)
        {
            var proof = await _witnessStore.GetProofAsync(scope.EndBlock);
            if (proof?.ProofBytes == null || proof.ProofBytes.Length == 0)
                return null;

            return new AnchorPayloadSection
            {
                Type = AnchorPayloadSectionType.InlineProof,
                Bytes = Proving.BlockProofSubmitter.SerializeProofPayload(proof)
            };
        }
    }
}
