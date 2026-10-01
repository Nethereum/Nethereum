using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    public static partial class SnapBootstrapper
    {
        public static async Task EnsureBytecodeCompleteAsync(
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, byte[] stateRoot,
            ILogger logger, CancellationToken ct)
        {
            if (scheduler == null || stateRoot == null || stateRoot.Length != 32) return;

            Nethereum.Merkle.Patricia.Storage.ITrieNodeStore nodes = bundle.StateTrieNodes;
            if (!Nethereum.Util.ByteUtil.AreEqual(stateRoot, DefaultValues.EMPTY_TRIE_HASH) && !nodes.ContainsKey(stateRoot))
            {
                logger.LogWarning("snap.bytecode.completeness - state trie root is not present in the store; blocking pivot commit.");
                throw new InvalidOperationException(
                    "Snap bytecode completeness gate could not load the state trie before pivot commit.");
            }

            Nethereum.Merkle.Patricia.PatriciaTrie trie;
            try { trie = Nethereum.Merkle.Patricia.PatriciaTrie.LoadFromStorage(stateRoot, nodes); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "snap.bytecode.completeness - state trie root is not loadable from the store; blocking pivot commit.");
                throw new InvalidOperationException(
                    "Snap bytecode completeness gate could not load the state trie before pivot commit.", ex);
            }

            var seen = new System.Collections.Generic.HashSet<byte[]>(Nethereum.Util.ByteArrayComparer.Current);
            var remaining = new System.Collections.Generic.HashSet<byte[]>(Nethereum.Util.ByteArrayComparer.Current);
            var decoder = new AccountEncoder();
            const long progressIntervalAccounts = 1_000_000;
            long accountsScanned = 0;
            var scanSw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                foreach (var entry in Nethereum.Merkle.Patricia.Proofs.PatriciaRangeIterator.EnumerateRange(
                    trie.Root, nodes, new byte[32]))
                {
                    ct.ThrowIfCancellationRequested();
                    if (++accountsScanned % progressIntervalAccounts == 0)
                        logger.LogInformation(
                            "snap.bytecode.completeness scanning accounts={Accounts:N0} missing={Missing:N0} elapsed={Elapsed}",
                            accountsScanned, remaining.Count, scanSw.Elapsed);
                    Account acc;
                    try { acc = decoder.Decode(entry.Value); }
                    catch (Exception ex) { logger.LogDebug(ex, "snap.bytecode.completeness - could not decode an account entry; skipping it."); continue; }
                    if (acc?.CodeHash == null || acc.CodeHash.Length != 32) continue;
                    if (Nethereum.Util.ByteUtil.AreEqual(acc.CodeHash, DefaultValues.EMPTY_DATA_HASH)) continue;
                    if (!seen.Add(acc.CodeHash)) continue;
                    var existing = await bundle.State.GetCodeAsync(acc.CodeHash).ConfigureAwait(false);
                    if (existing == null || existing.Length == 0) remaining.Add(acc.CodeHash);
                }
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "snap.bytecode.completeness - state trie walk hit an unreadable node before pivot commit; blocking pivot commit.");
                throw new InvalidOperationException(
                    "Snap bytecode completeness gate could not walk the state trie before pivot commit.", ex);
            }
            if (remaining.Count == 0) return;

            logger.LogInformation("snap.bytecode.completeness resume - fetching {Count} missing contract codes", remaining.Count);
            var fetched = await FetchAndWriteMissingBytecodesAsync(bundle, scheduler, remaining, 2UL * 1024 * 1024, logger, ct)
                .ConfigureAwait(false);
            if (remaining.Count > 0)
            {
                logger.LogWarning("snap.bytecode.completeness {Count} codes unresolved this attempt - blocking pivot commit", remaining.Count);
                throw new InvalidOperationException(
                    $"Snap bytecode completeness gate failed before pivot commit: {remaining.Count} contract code blob(s) remain unresolved.");
            }

            logger.LogInformation("snap.bytecode.completeness fetched {Fetched} contract codes", fetched);
        }
        public static async Task FetchMissingBytecodeAsync(
            IChainStoreBundle bundle, IFetchRequestScheduler scheduler, SnapSyncState resumeFrom,
            byte[] pivotStateRoot, ILogger logger, CancellationToken ct,
            System.Collections.Generic.IReadOnlyList<byte[]> phase2DeferredCode = null)
        {
            if (scheduler != null)
            {
                var union = new System.Collections.Generic.HashSet<byte[]>(Nethereum.Util.ByteArrayComparer.Current);
                if (phase2DeferredCode != null)
                    foreach (var h in phase2DeferredCode) union.Add(h);
                foreach (var h in DeferredHealCodeCodec.Decode(bundle.Metadata.GetDeferredHealCodeBlob()))
                    union.Add(h);

                var encodedUnion = DeferredHealCodeCodec.Encode(new System.Collections.Generic.List<byte[]>(union));
                if (encodedUnion.Length > 0)
                {
                    bundle.Metadata.SaveDeferredHealCodeBlob(encodedUnion);

                    var remaining = new System.Collections.Generic.List<byte[]>();
                    foreach (var h in union)
                    {
                        if (h is not { Length: 32 } || Nethereum.Util.ByteUtil.AreEqual(h, DefaultValues.EMPTY_DATA_HASH)) continue;
                        var existing = await bundle.State.GetCodeAsync(h).ConfigureAwait(false);
                        if (existing == null || existing.Length == 0) remaining.Add(h);
                    }
                    if (remaining.Count == 0)
                    {
                        bundle.Metadata.ClearDeferredHealCodeBlob();
                    }
                    else
                    {
                        logger.LogInformation(
                            "snap.phase3.bytecode_backstop healing {Count} contract code(s) deferred by Phase 2 (dead-ended peers) before pivot commit", remaining.Count);
                        var codeHealer = CreateHealer(bundle, scheduler, logger, null);
                        await codeHealer.HealAsync(pivotStateRoot, seedCodeHeal: remaining, ct: ct).ConfigureAwait(false);
                        int stillMissing = 0;
                        foreach (var h in remaining)
                        {
                            var c = await bundle.State.GetCodeAsync(h).ConfigureAwait(false);
                            if (c == null || c.Length == 0) stillMissing++;
                        }
                        if (stillMissing > 0)
                        {
                            logger.LogWarning(
                                "snap.phase3.bytecode_backstop {Remaining} of {Requested} deferred code(s) still unresolved - keeping durable list and blocking pivot commit",
                                stillMissing, union.Count);
                            throw new InvalidOperationException(
                                $"Snap Phase-3 bytecode backstop could not heal {stillMissing} Phase-2-deferred contract code(s) before pivot commit.");
                        }
                        bundle.Metadata.ClearDeferredHealCodeBlob();
                        logger.LogInformation("snap.phase3.bytecode_backstop healed {Count} deferred contract code(s)", remaining.Count);
                    }
                }
            }

            if (scheduler != null && bundle is IFlatStateReconciler codeInventory)
            {
                var missing = codeInventory.GetPersistedMissingCode();
                if (missing.Count > 0)
                {
                    logger.LogInformation("snap.bytecode.fetch {Count} missing code blob(s) named by the sweep", missing.Count);
                    var remaining = new System.Collections.Generic.HashSet<byte[]>(missing, Nethereum.Util.ByteArrayComparer.Current);
                    var alreadyWritten = new System.Collections.Generic.List<byte[]>();
                    foreach (var hash in remaining)
                    {
                        var existing = await bundle.State.GetCodeAsync(hash).ConfigureAwait(false);
                        if (existing != null && existing.Length > 0) alreadyWritten.Add(hash);
                    }
                    foreach (var hash in alreadyWritten) remaining.Remove(hash);

                    var fetched = await FetchAndWriteMissingBytecodesAsync(bundle, scheduler, remaining, 512 * 1024, logger, ct)
                        .ConfigureAwait(false);
                    if (remaining.Count > 0)
                    {
                        logger.LogWarning(
                            "snap.bytecode.fetch {Remaining} of {Requested} code blob(s) unresolved this attempt - keeping inventory and blocking pivot commit",
                            remaining.Count,
                            missing.Count);
                        throw new InvalidOperationException(
                            $"Snap bytecode fetch failed before pivot commit: {remaining.Count} contract code blob(s) remain unresolved.");
                    }

                    logger.LogInformation("snap.bytecode.fetch wrote {Fetched} contract code blob(s)", fetched);
                    codeInventory.ClearPersistedMissingCode();
                    return;
                }
            }

            if (resumeFrom != null && scheduler != null)
            {
                await EnsureBytecodeCompleteAsync(bundle, scheduler, pivotStateRoot, logger, ct)
                    .ConfigureAwait(false);
            }
        }
        private static void PersistDeferredHealCode(
            IChainMetadataStore metadata, System.Collections.Generic.IReadOnlyList<byte[]> codes)
        {
            if (metadata == null || codes == null || codes.Count == 0) return;
            var union = new System.Collections.Generic.HashSet<byte[]>(Nethereum.Util.ByteArrayComparer.Current);
            foreach (var h in DeferredHealCodeCodec.Decode(metadata.GetDeferredHealCodeBlob())) union.Add(h);
            foreach (var h in codes) if (h is { Length: 32 }) union.Add(h);
            if (union.Count == 0) return;
            metadata.SaveDeferredHealCodeBlob(
                DeferredHealCodeCodec.Encode(new System.Collections.Generic.List<byte[]>(union)));
        }
        private static async Task<long> FetchAndWriteMissingBytecodesAsync(
            IChainStoreBundle bundle,
            IFetchRequestScheduler scheduler,
            System.Collections.Generic.HashSet<byte[]> remaining,
            ulong responseBytes,
            ILogger logger,
            CancellationToken ct)
        {
            if (remaining.Count == 0) return 0;

            var keccak = Nethereum.Util.Sha3Keccack.Current;
            const int maxCodeRequestCount = 84;
            const int maxNoProgress = 16;
            int noProgress = 0;
            long fetched = 0;
            while (remaining.Count > 0 && noProgress < maxNoProgress)
            {
                ct.ThrowIfCancellationRequested();
                int before = remaining.Count;
                var toFetch = new System.Collections.Generic.List<byte[]>(remaining);
                for (int off = 0; off < toFetch.Count; off += maxCodeRequestCount)
                {
                    ct.ThrowIfCancellationRequested();
                    int take = Math.Min(maxCodeRequestCount, toFetch.Count - off);
                    var chunk = toFetch.GetRange(off, take);
                    Nethereum.Model.P2P.Snap.ByteCodesMessage resp;
                    try { resp = await scheduler.FetchByteCodesAsync(chunk, responseBytes, ct).ConfigureAwait(false); }
                    catch (FetchRequestFailedException ex)
                    {
                        logger.LogDebug(ex, "snap.bytecode.fetch failed for a chunk of {Count} code hash(es)", chunk.Count);
                        continue;
                    }

                    foreach (var code in resp.Codes)
                    {
                        if (code == null || code.Length == 0) continue;
                        var h = keccak.CalculateHash(code);
                        if (!remaining.Remove(h)) continue;
                        await bundle.State.SaveCodeAsync(h, code).ConfigureAwait(false);
                        fetched++;
                    }
                }

                if (remaining.Count < before) noProgress = 0;
                else
                {
                    noProgress++;
                    if (remaining.Count > 0) await Task.Delay(500, ct).ConfigureAwait(false);
                }
            }

            return fetched;
        }
    }
}
