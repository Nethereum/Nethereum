using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Model;
using Nethereum.Util;
using HealResult = Nethereum.DevP2P.Sync.Snap.Healing.TrieHealer.HealResult;

namespace Nethereum.DevP2P.Sync.Snap.Healing
{
    internal sealed class HealSession
    {
        internal HealSession(HealStats stats, byte[] targetRoot, IReadOnlyList<byte[]> codeSeeds)
        {
            Stats = stats;
            LiveTargetRoot = targetRoot;
            CodeSeeds = codeSeeds;
        }

        internal HealStats Stats { get; }
        internal IReadOnlyList<byte[]> CodeSeeds;
        internal byte[] LiveTargetRoot;
        internal ulong CurrentPivotBlock;
        internal bool HasCodeSeeds;
        internal int StallRounds;
        internal bool CommittedEver;
        private HashSet<string> _forcedPrefixes;
        internal int Round;
        internal int TotalNodesFetched;
        internal System.Diagnostics.Stopwatch PivotPollSw;

        internal bool TargetIsEmptyTrie => ByteUtil.AreEqual(LiveTargetRoot, DefaultValues.EMPTY_TRIE_HASH);

        internal void Configure(
            ulong pivotBlock,
            Nethereum.CoreChain.Storage.IStateStore? codeStore,
            ILogger logger)
        {
            CurrentPivotBlock = pivotBlock;
            HasCodeSeeds = CodeSeeds != null && CodeSeeds.Count > 0;
            if (HasCodeSeeds && codeStore == null)
            {
                logger.LogError(
                    "heal.code.seed {Count} bytecode heal seed(s) supplied but no code store is wired — these codes cannot be healed",
                    CodeSeeds.Count);
                HasCodeSeeds = false;
            }
            StallRounds = 0;
            CommittedEver = false;
        }

        internal void ForceDescentTowards(IReadOnlyCollection<byte[]> accountHashes)
        {
            if (accountHashes == null || accountHashes.Count == 0) return;
            _forcedPrefixes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var account in accountHashes)
            {
                if (account is not { Length: 32 }) continue;
                var nibbles = account.ConvertToNibbles();
                for (var length = 1; length <= nibbles.Length; length++)
                    _forcedPrefixes.Add(NibbleKey(nibbles, length));
            }
        }

        internal bool ForcesDescentAt(byte[] nibblePath)
            => _forcedPrefixes != null && nibblePath.Length > 0 && _forcedPrefixes.Contains(NibbleKey(nibblePath, nibblePath.Length));

        private static string NibbleKey(byte[] nibbles, int length)
            => Convert.ToHexString(nibbles, 0, length);

        internal HealResult NotConverged(HealStats stats)
        {
            return new HealResult(false, TotalNodesFetched, LiveTargetRoot, Array.Empty<byte>(),
                stats.PrunedChildren, stats.RequiredAbsent, stats.RequiredStale);
        }

        internal HealResult Converged(HealStats stats, bool matched)
        {
            return new HealResult(matched, TotalNodesFetched, LiveTargetRoot,
                matched ? LiveTargetRoot : Array.Empty<byte>(),
                stats.PrunedChildren, stats.RequiredAbsent, stats.RequiredStale);
        }

        internal HealResult Retarget(HealStats stats, byte[] root, ulong block)
        {
            return new HealResult(false, TotalNodesFetched, LiveTargetRoot, Array.Empty<byte>(),
                stats.PrunedChildren, stats.RequiredAbsent, stats.RequiredStale,
                NeedsRetarget: true, RetargetRoot: root, RetargetBlock: block);
        }

        internal void BeginRounds()
        {
            Round = 0;
            TotalNodesFetched = 0;
            PivotPollSw = System.Diagnostics.Stopwatch.StartNew();
        }

        internal void RecordRoundProgress(int processed, long nodesAddedThisRound, bool codeProgress)
        {
            if (nodesAddedThisRound > 0) CommittedEver = true;
            bool madeProgress = (CommittedEver ? nodesAddedThisRound > 0 : processed > 0) || codeProgress;
            if (madeProgress) StallRounds = 0; else StallRounds++;
        }

        internal static IReadOnlyList<byte[]> SanitizeCodeSeeds(IReadOnlyList<byte[]> seedCodeHeal)
        {
            if (seedCodeHeal == null || seedCodeHeal.Count == 0) return seedCodeHeal;
            var seen = new HashSet<byte[]>(ByteArrayComparer.Current);
            var valid = new List<byte[]>(seedCodeHeal.Count);
            foreach (var h in seedCodeHeal)
            {
                if (h is not { Length: 32 }) continue;
                if (ByteUtil.AreEqual(h, DefaultValues.EMPTY_DATA_HASH)) continue;
                if (seen.Add(h)) valid.Add(h);
            }
            return valid;
        }
    }
}
