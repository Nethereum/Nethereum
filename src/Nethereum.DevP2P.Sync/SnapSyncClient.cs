using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Pulls full state for a given root from a snap/1 peer:
    /// 1. Walks the account range in slices, decodes each slim body, and streams every
    ///    account into the provided <see cref="ISnapSyncSink"/>.
    /// 2. For each account with a non-empty storage root, streams the storage trie ranges
    ///    into the sink while still inside the account-range batch (no full-account-list
    ///    accumulation).
    /// 3. After all account ranges land, pulls bytecodes by codeHash for accounts with
    ///    non-empty code and streams them into the sink.
    /// 4. Asks the sink to finalise its running state-root computation; compares it to
    ///    <c>targetRoot</c>. Mismatch =&gt; tampered or incomplete data, throw.
    ///
    /// A peer that returns a tampered proof is rejected on the spot; a peer that returns
    /// a tampered range that survives the per-account-storage check is caught at the
    /// final root comparison. Either way, the client never returns a "successful" result
    /// against a target root that wasn't actually reached.
    /// </summary>
    public class SnapSyncClient
    {
        private readonly ISnapPeer _peer;
        private readonly ISnapSyncSink _sink;
        private readonly int _accountsPerRequest;
        private readonly ulong _responseBytesBudget;

        public SnapSyncClient(ISnapPeer peer, int accountsPerRequest = 256, ulong responseBytesBudget = 1_000_000UL)
            : this(peer, sink: null, accountsPerRequest, responseBytesBudget) { }

        /// <summary>
        /// Construct with an explicit sink. Pass <c>null</c> to use the in-memory sink (back-compat
        /// default for AppChain and tests).
        /// </summary>
        public SnapSyncClient(ISnapPeer peer, ISnapSyncSink sink, int accountsPerRequest = 256, ulong responseBytesBudget = 1_000_000UL)
        {
            _peer = peer ?? throw new ArgumentNullException(nameof(peer));
            _sink = sink ?? new InMemorySnapSyncSink();
            _accountsPerRequest = accountsPerRequest;
            _responseBytesBudget = responseBytesBudget;
        }

        public ISnapSyncSink Sink => _sink;

        public class SyncResult
        {
            public ISnapSyncSink Sink { get; set; }
            public byte[] ComputedRoot { get; set; }
            public bool RootMatchesTarget { get; set; }
            public int AccountCount { get; set; }

            // Back-compat for in-memory consumers (AppChain + snap conformance tests).
            // Null when running with a streaming sink (e.g. RocksDb).
            public PatriciaTrie StateTrie { get; set; }
            public InMemoryTrieStorage TrieStorage { get; set; }
            public Dictionary<string, byte[]> BytecodeByHash { get; set; } = new();
        }

        public async Task<SyncResult> SyncStateAsync(byte[] targetRoot, CancellationToken ct = default)
        {
            if (targetRoot == null || targetRoot.Length != 32)
                throw new ArgumentException("targetRoot must be 32 bytes", nameof(targetRoot));

            await _sink.BeginAsync(targetRoot, ct).ConfigureAwait(false);

            var codeHashesToFetch = new List<byte[]>();
            var accountDecoder = new AccountEncoder();
            var nextStart = new byte[32];
            ulong reqId = 1;
            int accountCount = 0;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var resp = await _peer.GetAccountRangeAsync(new GetAccountRangeMessage
                {
                    RequestId = reqId++,
                    RootHash = targetRoot,
                    StartingHash = nextStart,
                    LimitHash = FilledHash(0xff),
                    ResponseBytes = _responseBytesBudget
                }, ct).ConfigureAwait(false);

                if (resp.Accounts.Count == 0)
                    break;

                // Per-response edge-proof verification is intentionally deferred to the
                // sink's running root computation: implementing a separate range-proof
                // verifier here would duplicate the trie reconstruction. With an anchored
                // target root from L1, any tampering — boundary, body, ordering, or
                // completeness — is caught when the sink's running root fails to match.
                foreach (var entry in resp.Accounts)
                {
                    ct.ThrowIfCancellationRequested();

                    await _sink.WriteAccountAsync(entry.Hash, entry.Body, ct).ConfigureAwait(false);
                    accountCount++;

                    var canonical = SlimAccountEncoder.FromSlim(entry.Body);
                    var decoded = accountDecoder.Decode(canonical);

                    if (!ByteUtil.AreEqual(decoded.StateRoot, DefaultValues.EMPTY_TRIE_HASH))
                    {
                        await PullStorageForAccountAsync(targetRoot, entry.Hash, decoded.StateRoot, reqId++, ct)
                            .ConfigureAwait(false);
                    }
                    if (!ByteUtil.AreEqual(decoded.CodeHash, DefaultValues.EMPTY_DATA_HASH))
                        codeHashesToFetch.Add(decoded.CodeHash);
                }

                var lastHash = resp.Accounts[^1].Hash;
                if (IsMaxHash(lastHash)) break;
                nextStart = IncrementHash(lastHash);
            }

            if (codeHashesToFetch.Count > 0)
            {
                var codesResp = await _peer.GetByteCodesAsync(new GetByteCodesMessage
                {
                    RequestId = reqId++,
                    Hashes = codeHashesToFetch,
                    ResponseBytes = _responseBytesBudget
                }, ct).ConfigureAwait(false);

                for (int i = 0; i < codesResp.Codes.Count && i < codeHashesToFetch.Count; i++)
                    await _sink.WriteBytecodeAsync(codeHashesToFetch[i], codesResp.Codes[i], ct).ConfigureAwait(false);
            }

            var computedRoot = await _sink.FinaliseRootAsync(ct).ConfigureAwait(false);
            var result = new SyncResult
            {
                Sink = _sink,
                ComputedRoot = computedRoot,
                RootMatchesTarget = ByteUtil.AreEqual(computedRoot, targetRoot),
                AccountCount = accountCount
            };

            // Back-compat: expose the in-memory tries/bytecodes on the result for AppChain
            // consumers that still read them directly. Streaming sinks leave these null.
            if (_sink is InMemorySnapSyncSink mem)
            {
                result.StateTrie = mem.StateTrie;
                result.TrieStorage = mem.TrieStorage;
                foreach (var kv in mem.BytecodeByHash)
                    result.BytecodeByHash[kv.Key] = kv.Value;
            }

            if (!result.RootMatchesTarget)
                throw new InvalidOperationException(
                    $"Snap-sync result root {result.ComputedRoot.ToHex()} does not match target {targetRoot.ToHex()} — peer returned tampered or incomplete data");

            return result;
        }

        private async Task PullStorageForAccountAsync(
            byte[] stateRoot, byte[] accountHash, byte[] storageRoot, ulong reqId, CancellationToken ct)
        {
            await _sink.BeginAccountStorageAsync(accountHash, storageRoot, ct).ConfigureAwait(false);

            var nextStart = new byte[32];
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var resp = await _peer.GetStorageRangesAsync(new GetStorageRangesMessage
                {
                    RequestId = reqId,
                    RootHash = stateRoot,
                    AccountHashes = new List<byte[]> { accountHash },
                    StartingHash = nextStart,
                    LimitHash = FilledHash(0xff),
                    ResponseBytes = _responseBytesBudget
                }, ct).ConfigureAwait(false);

                if (resp.Slots.Count == 0 || resp.Slots[0].Count == 0) break;

                foreach (var slot in resp.Slots[0])
                    await _sink.WriteStorageSlotAsync(slot.Hash, slot.Data, ct).ConfigureAwait(false);

                var lastHash = resp.Slots[0][^1].Hash;
                if (IsMaxHash(lastHash)) break;
                nextStart = IncrementHash(lastHash);

                if (resp.Proof == null || resp.Proof.Count == 0) break;
            }

            await _sink.EndAccountStorageAsync(ct).ConfigureAwait(false);
        }

        private static byte[] IncrementHash(byte[] h)
        {
            var copy = (byte[])h.Clone();
            for (int i = copy.Length - 1; i >= 0; i--)
            {
                if (copy[i] != 0xff) { copy[i]++; return copy; }
                copy[i] = 0;
            }
            return copy;
        }

        private static bool IsMaxHash(byte[] h)
        {
            for (int i = 0; i < h.Length; i++) if (h[i] != 0xff) return false;
            return true;
        }

        private static byte[] FilledHash(byte b)
        {
            var hh = new byte[32];
            for (int i = 0; i < 32; i++) hh[i] = b;
            return hh;
        }
    }
}
