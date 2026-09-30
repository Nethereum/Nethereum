using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Validation;

namespace Nethereum.AppChain.Anchoring.Finality
{
    public sealed class AnchorTargetCanonicalSource : ICanonicalStateRootSource
    {
        private readonly AppChainAnchoredFinalitySource _anchors;

        public AnchorTargetCanonicalSource(AppChainAnchoredFinalitySource anchors)
        {
            _anchors = anchors ?? throw new ArgumentNullException(nameof(anchors));
        }

        public string Name => "AppChainAnchorTarget";

        public async Task<CanonicalTip> GetLatestAsync(CancellationToken ct) =>
            await _anchors.GetAnchorTargetAsync(ct).ConfigureAwait(false);

        public async Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(
            ulong blockNumber,
            CancellationToken ct)
        {
            var target = await _anchors.GetAnchorTargetAsync(ct).ConfigureAwait(false);
            return target != null && target.BlockNumber == blockNumber
                ? (target.StateRoot, target.BlockHash)
                : (null, null);
        }
    }
}
