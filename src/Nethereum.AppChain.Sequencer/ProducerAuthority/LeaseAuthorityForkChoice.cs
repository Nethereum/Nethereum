using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.AppChain.Sequencer.ProducerAuthority
{
    public sealed class LeaseAuthorityForkChoice : IChainForkChoice
    {
        private readonly ISequencerArbiter _arbiter;
        private readonly IBlockStore _blocks;

        public LeaseAuthorityForkChoice(ISequencerArbiter arbiter, IBlockStore blocks)
        {
            _arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter));
            _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
        }

        public Task<ForkChoiceVerdict> ShouldAdoptAsync(BlockHeader incomingHead, byte[] incomingHash, CancellationToken ct)
            => ShouldAdoptAsync(incomingHead, incomingHash, sourcePeerNodeId: null, ct);

        public async Task<ForkChoiceVerdict> ShouldAdoptAsync(
            BlockHeader incomingHead, byte[] incomingHash, string sourcePeerNodeId, CancellationToken ct)
        {
            if (incomingHead == null) throw new ArgumentNullException(nameof(incomingHead));
            if (incomingHash == null) throw new ArgumentNullException(nameof(incomingHash));

            if (await IsOnOurCanonicalChainAsync(incomingHead.BlockNumber, incomingHash).ConfigureAwait(false))
                return ForkChoiceVerdict.KeepLocal("already canonical");

            if (string.IsNullOrEmpty(sourcePeerNodeId))
                return ForkChoiceVerdict.Undecidable(
                    "the incoming branch's source peer is unknown; cannot attribute it to the arbiter's current holder");

            var status = await _arbiter.CurrentAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(status.NodeId))
                return ForkChoiceVerdict.Undecidable("the arbiter names no current fencing-token holder");

            if (!string.Equals(sourcePeerNodeId, status.NodeId, StringComparison.OrdinalIgnoreCase))
                return ForkChoiceVerdict.Undecidable(
                    $"incoming branch's source peer '{sourcePeerNodeId}' does not match the arbiter's " +
                    $"current fencing-token holder '{status.NodeId}'");

            var ancestor = incomingHead.BlockNumber > 0 ? (ulong)incomingHead.BlockNumber - 1 : 0UL;
            return ForkChoiceVerdict.AdoptIncoming(
                ancestor, $"incoming branch's source peer holds the current fencing token (fencing={status.FencingToken})");
        }

        private async Task<bool> IsOnOurCanonicalChainAsync(System.Numerics.BigInteger number, byte[] hash)
        {
            var ours = await _blocks.GetHashByNumberAsync(number).ConfigureAwait(false);
            return ours != null && ByteUtil.AreEqual(ours, hash);
        }
    }
}
