using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.DataAvailability
{
    public class CalldataDataContributor : IAnchorPayloadContributor
    {
        private readonly IWitnessStore _witnessStore;
        private readonly CompressionAlgo _compression;

        public CalldataDataContributor(IWitnessStore witnessStore, CompressionAlgo compression = CompressionAlgo.None)
        {
            _witnessStore = witnessStore;
            _compression = compression;
        }

        public AnchorPayloadSectionType Kind =>
            _compression == CompressionAlgo.None
                ? AnchorPayloadSectionType.InlineDa
                : AnchorPayloadSectionType.CompressedCalldata;

        public async Task<AnchorPayloadSection> ContributeAsync(AnchorScope scope, CancellationToken ct = default)
        {
            var witness = await _witnessStore.GetWitnessAsync(scope.EndBlock);
            if (witness == null || witness.Length == 0)
                return null;

            if (_compression == CompressionAlgo.None)
            {
                return new AnchorPayloadSection
                {
                    Type = AnchorPayloadSectionType.InlineDa,
                    Bytes = witness
                };
            }

            var envelope = CompressedEnvelope.Wrap(witness, _compression);

            return new AnchorPayloadSection
            {
                Type = AnchorPayloadSectionType.CompressedCalldata,
                Bytes = envelope
            };
        }
    }
}
