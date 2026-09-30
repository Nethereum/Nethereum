using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
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

            var results = new IReadOnlyList<AccountChanges>[blocks.Count];
            var indexByHash = new Dictionary<string, int>(blocks.Count);
            var pending = new List<BalBlockRef>(blocks.Count);
            for (var i = 0; i < blocks.Count; i++)
            {
                indexByHash[blocks[i].BlockHash.ToHex()] = i;
                pending.Add(blocks[i]);
            }
            var refusedBy = new Dictionary<string, HashSet<string>>();
            var statelessPeers = new HashSet<string>();

            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var connected = _peers.GetServiceablePeers() ?? Array.Empty<IBlockAccessListPeer>();
                var serviceable = connected.Where(p => !statelessPeers.Contains(p.Id)).ToList();

                EnsureProgressPossible(pending, refusedBy, connected, serviceable);

                if (serviceable.Count == 0)
                {
                    await WaitForPeerChangeAsync(ct).ConfigureAwait(false);
                    continue;
                }

                var changed = false;
                foreach (var peer in serviceable)
                {
                    if (statelessPeers.Contains(peer.Id)) continue;

                    var batch = TakeBatch(pending, refusedBy, peer.Id);
                    if (batch.Count == 0) continue;

                    IReadOnlyList<byte[]> raw;
                    try
                    {
                        raw = await peer.RequestBlockAccessListsAsync(
                            batch.Select(b => b.BlockHash).ToList(), ResponseByteBudget, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch { continue; }

                    changed |= ApplyResponse(
                        batch, raw ?? Array.Empty<byte[]>(), peer.Id, indexByHash, results, pending, refusedBy, statelessPeers);
                }

                if (!changed)
                    await WaitForPeerChangeAsync(ct).ConfigureAwait(false);
            }
            return results;
        }

        private async Task WaitForPeerChangeAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(PeerWaitInterval, ct).ConfigureAwait(false);
        }

        private static List<BalBlockRef> TakeBatch(
            List<BalBlockRef> pending, Dictionary<string, HashSet<string>> refusedBy, string peerId)
        {
            var batch = new List<BalBlockRef>(MaxHashesPerRequest);
            foreach (var block in pending)
            {
                if (refusedBy.TryGetValue(block.BlockHash.ToHex(), out var refusers) && refusers.Contains(peerId))
                    continue;
                batch.Add(block);
                if (batch.Count == MaxHashesPerRequest) break;
            }
            return batch;
        }

        private bool ApplyResponse(
            List<BalBlockRef> batch, IReadOnlyList<byte[]> raw, string peerId,
            Dictionary<string, int> indexByHash, IReadOnlyList<AccountChanges>[] results,
            List<BalBlockRef> pending, Dictionary<string, HashSet<string>> refusedBy, HashSet<string> statelessPeers)
        {
            if (raw.Count > batch.Count)
                return statelessPeers.Add(peerId);
            if (raw.Count == 0)
                return statelessPeers.Add(peerId);

            var verified = _verifier.Verify(batch.Select(b => b.BlockAccessListHash).ToList(), raw);
            var changed = false;
            for (var i = 0; i < verified.Count; i++)
            {
                var block = batch[i];
                switch (verified[i].Status)
                {
                    case BlockAccessListStatus.Verified:
                        results[indexByHash[block.BlockHash.ToHex()]] = verified[i].AccessList;
                        pending.Remove(block);
                        changed = true;
                        break;
                    case BlockAccessListStatus.HashMismatch:
                        changed |= statelessPeers.Add(peerId);
                        break;
                    default:
                        changed |= Refuse(refusedBy, block.BlockHash.ToHex(), peerId);
                        break;
                }
            }
            return changed;
        }

        private static void EnsureProgressPossible(
            List<BalBlockRef> pending, Dictionary<string, HashSet<string>> refusedBy,
            IReadOnlyList<IBlockAccessListPeer> connected, IReadOnlyList<IBlockAccessListPeer> serviceable)
        {
            if (connected.Count == 0) return;
            if (serviceable.Count == 0)
                throw new InvalidOperationException(
                    "BAL catch-up: every connected peer has been excluded for misbehaviour; none can serve block access lists.");

            EnsureObtainable(pending, refusedBy, serviceable);
        }

        private static void EnsureObtainable(
            List<BalBlockRef> pending, Dictionary<string, HashSet<string>> refusedBy, IReadOnlyList<IBlockAccessListPeer> peers)
        {
            var peerIds = peers.Select(p => p.Id).ToHashSet();
            foreach (var block in pending)
            {
                if (refusedBy.TryGetValue(block.BlockHash.ToHex(), out var refusers) && peerIds.IsSubsetOf(refusers))
                    throw new InvalidOperationException(
                        $"BAL catch-up: block 0x{block.BlockHash.ToHex()} access list is unavailable from every serviceable peer.");
            }
        }

        private static bool Refuse(Dictionary<string, HashSet<string>> refusedBy, string blockHex, string peerId)
        {
            if (!refusedBy.TryGetValue(blockHex, out var set))
            {
                set = new HashSet<string>();
                refusedBy[blockHex] = set;
            }
            return set.Add(peerId);
        }
    }
}
