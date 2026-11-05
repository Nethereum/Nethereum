using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Snap.CatchUp
{
    internal sealed class BalFetchRound
    {
        public BalFetchRound(IReadOnlyList<BalBlockRef> blocks)
        {
            Results = new IReadOnlyList<AccountChanges>[blocks.Count];
            IndexByHash = new Dictionary<string, int>(blocks.Count);
            Pending = new List<BalBlockRef>(blocks.Count);
            for (var i = 0; i < blocks.Count; i++)
            {
                IndexByHash[blocks[i].BlockHash.ToHex()] = i;
                Pending.Add(blocks[i]);
            }
        }

        public IReadOnlyList<AccountChanges>[] Results { get; }
        public Dictionary<string, int> IndexByHash { get; }
        public List<BalBlockRef> Pending { get; }
        public Dictionary<string, HashSet<string>> RefusedBy { get; } = new Dictionary<string, HashSet<string>>();
        public HashSet<string> StatelessPeers { get; } = new HashSet<string>();

        private const int FaultsBeforeExclusion = 2;

        private Dictionary<string, int> ConsecutiveFaults { get; } = new Dictionary<string, int>();

        public bool RecordFault(string peerId, bool hasAlternative)
        {
            ConsecutiveFaults.TryGetValue(peerId, out var faults);
            ConsecutiveFaults[peerId] = faults + 1;
            return hasAlternative && faults + 1 >= FaultsBeforeExclusion && StatelessPeers.Add(peerId);
        }

        public List<BalBlockRef> TakeBatch(string peerId)
        {
            var batch = new List<BalBlockRef>(BlockAccessListFetcher.MaxHashesPerRequest);
            foreach (var block in Pending)
            {
                if (RefusedBy.TryGetValue(block.BlockHash.ToHex(), out var refusers) && refusers.Contains(peerId))
                    continue;
                batch.Add(block);
                if (batch.Count == BlockAccessListFetcher.MaxHashesPerRequest) break;
            }
            return batch;
        }

        public bool ApplyResponse(
            List<BalBlockRef> batch, IReadOnlyList<byte[]> raw, string peerId, BlockAccessListVerifier verifier)
        {
            if (raw.Count > batch.Count || raw.Count == 0)
                return StatelessPeers.Add(peerId);

            ConsecutiveFaults.Remove(peerId);

            var verified = verifier.Verify(batch.Select(b => b.BlockAccessListHash).ToList(), raw);
            var changed = false;
            for (var i = 0; i < verified.Count; i++)
            {
                var block = batch[i];
                switch (verified[i].Status)
                {
                    case BlockAccessListStatus.Verified:
                        Results[IndexByHash[block.BlockHash.ToHex()]] = verified[i].AccessList;
                        Pending.Remove(block);
                        changed = true;
                        break;
                    case BlockAccessListStatus.HashMismatch:
                        changed |= StatelessPeers.Add(peerId);
                        break;
                    default:
                        changed |= Refuse(block.BlockHash.ToHex(), peerId);
                        break;
                }
            }
            return changed;
        }

        public void EnsureObtainable(IReadOnlyList<IBlockAccessListPeer> peers)
        {
            var peerIds = peers.Select(p => p.Id).ToHashSet();
            foreach (var block in Pending)
            {
                if (RefusedBy.TryGetValue(block.BlockHash.ToHex(), out var refusers) && peerIds.IsSubsetOf(refusers))
                    throw new InvalidOperationException(
                        $"BAL catch-up: block 0x{block.BlockHash.ToHex()} access list is unavailable from every serviceable peer.");
            }
        }

        private bool Refuse(string blockHex, string peerId)
        {
            if (!RefusedBy.TryGetValue(blockHex, out var set))
            {
                set = new HashSet<string>();
                RefusedBy[blockHex] = set;
            }
            return set.Add(peerId);
        }
    }
}
