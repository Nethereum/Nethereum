using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Snap.CatchUp
{
    public sealed class BlockAccessListFetcher : IBlockAccessListFetcher
    {
        public const int MaxHashesPerRequest = 28;
        public const ulong ResponseByteBudget = 2 * 1024 * 1024;

        private readonly IBlockAccessListPeerSource _peers;
        private readonly BlockAccessListVerifier _verifier;

        public BlockAccessListFetcher(IBlockAccessListPeerSource peers, BlockAccessListVerifier verifier)
        {
            _peers = peers ?? throw new ArgumentNullException(nameof(peers));
            _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        }

        public TimeSpan PeerWaitInterval { get; set; } = TimeSpan.FromSeconds(1);

        public async Task<IReadOnlyList<IReadOnlyList<AccountChanges>>> FetchAsync(
            IReadOnlyList<BalBlockRef> blocks, CancellationToken ct)
        {
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));

            var round = new BalFetchRound(blocks);

            while (round.Pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var connected = _peers.GetServiceablePeers() ?? Array.Empty<IBlockAccessListPeer>();
                var serviceable = connected.Where(p => !round.StatelessPeers.Contains(p.Id)).ToList();

                EnsureProgressPossible(round, connected, serviceable);

                if (serviceable.Count == 0)
                {
                    await WaitForPeerChangeAsync(ct).ConfigureAwait(false);
                    continue;
                }

                var changed = false;
                foreach (var peer in serviceable)
                    changed |= await CollectFromPeerAsync(round, peer, serviceable.Count > 1, ct).ConfigureAwait(false);

                if (!changed)
                    await WaitForPeerChangeAsync(ct).ConfigureAwait(false);
            }
            return round.Results;
        }

        private async Task<bool> CollectFromPeerAsync(BalFetchRound round, IBlockAccessListPeer peer, bool hasAlternative, CancellationToken ct)
        {
            if (round.StatelessPeers.Contains(peer.Id)) return false;

            var batch = round.TakeBatch(peer.Id);
            if (batch.Count == 0) return false;

            IReadOnlyList<byte[]> raw;
            try
            {
                raw = await peer.RequestBlockAccessListsAsync(
                    batch.Select(b => b.BlockHash).ToList(), ResponseByteBudget, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { return round.RecordFault(peer.Id, hasAlternative); }

            return round.ApplyResponse(
                batch, raw ?? Array.Empty<byte[]>(), peer.Id, _verifier);
        }

        private async Task WaitForPeerChangeAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(PeerWaitInterval, ct).ConfigureAwait(false);
        }

        private static void EnsureProgressPossible(
            BalFetchRound round,
            IReadOnlyList<IBlockAccessListPeer> connected, IReadOnlyList<IBlockAccessListPeer> serviceable)
        {
            if (connected.Count == 0) return;
            if (serviceable.Count == 0)
                throw new InvalidOperationException(
                    "BAL catch-up: every connected peer has been excluded for misbehaviour; none can serve block access lists.");

            round.EnsureObtainable(serviceable);
        }
    }
}
