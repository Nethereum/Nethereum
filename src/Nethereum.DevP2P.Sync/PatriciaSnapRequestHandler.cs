using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// snap/1 server-side handler backed by a Patricia state trie and a bytecode store.
    /// Spec: https://github.com/ethereum/devp2p/blob/master/caps/snap.md.
    /// Same machinery serves three things at once:
    /// - go-ethereum's <c>devp2p rlpx snap-test</c> conformance suite (5 sub-tests)
    /// - AppChain fast-follower state bootstrap (sync state without re-executing all blocks)
    /// - Indexer cold start (walk state at a specific anchored block root)
    ///
    /// Trust model for AppChain followers: the receiver checks the resulting
    /// state root against the L1 anchor record before accepting the synced state.
    /// </summary>
    public class PatriciaSnapRequestHandler : ISnapRequestHandler
    {
        /// <summary>
        /// Server-side soft cap on the response budget for every snap/1 request type
        /// (GetAccountRange 0x00, GetStorageRanges 0x02, GetByteCodes 0x04,
        /// GetTrieNodes 0x06). The snap/1 spec defines <c>responseBytes</c> as a peer
        /// hint and explicitly allows the server to return less data based on its own
        /// QoS limits; this constant pins the upper bound the server will ever honour
        /// regardless of the peer-supplied value, mirroring the geth interop invariant
        /// <c>eth/protocols/snap/handler.go:32</c> (<c>softResponseLimit = 2 * 1024 * 1024</c>).
        /// </summary>
        public const int SoftResponseLimit = 2 * 1024 * 1024;

        /// <summary>
        /// Hard count cap on the number of code entries returned per GetByteCodes
        /// response. Required because EMPTY_DATA_HASH responses contribute zero
        /// bytes to the byte-budget gate, so a peer-controlled hashlist of N×EMPTY
        /// cannot be bounded by bytes alone. Geth interop invariant
        /// <c>eth/protocols/snap/handler.go</c> (<c>maxCodeLookups = 1024</c>).
        /// </summary>
        public const int MaxCodeLookups = 1024;

        /// <summary>
        /// Hard count cap on the total number of trie-node blobs returned per
        /// GetTrieNodes response, summed across all pathsets in the request. Geth
        /// interop invariant <c>eth/protocols/snap/handler.go</c>
        /// (<c>maxTrieNodeLookups = 1024</c>).
        /// </summary>
        public const int MaxTrieNodeLookups = 1024;

        /// <summary>
        /// Wall-clock cap on a single GetTrieNodes service call. Geth interop
        /// invariant <c>eth/protocols/snap/handler.go</c>
        /// (<c>maxTrieNodeTimeSpent = 5 * time.Second</c>).
        /// </summary>
        public static readonly TimeSpan MaxTrieNodeTimeSpent = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Multiplier added to the soft byte budget when sizing the GetStorageRanges
        /// hard cap, so a single contract's slots can slightly exceed the soft cap
        /// without forcing the caller to issue a continuation just to retrieve one
        /// in-flight contract atomically. Geth interop invariant
        /// <c>eth/protocols/snap/handler.go</c> (<c>stateLookupSlack = 0.1</c>).
        /// </summary>
        public const double StateLookupSlack = 0.1;

        private readonly ITrieStorage _stateNodes;
        private readonly IBytecodeStore _bytecodes;
        private readonly Func<byte[], byte[]> _accountStateRoot;

        public PatriciaSnapRequestHandler(
            ITrieStorage stateNodeStore,
            IBytecodeStore bytecodeStore,
            Func<byte[], byte[]> accountStateRootProvider = null)
        {
            _stateNodes = stateNodeStore ?? throw new ArgumentNullException(nameof(stateNodeStore));
            _bytecodes = bytecodeStore ?? throw new ArgumentNullException(nameof(bytecodeStore));
            _accountStateRoot = accountStateRootProvider;
        }

        public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage req, CancellationToken ct = default)
        {
            try
            {
                return GetAccountRangeCoreAsync(req, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (OutOfMemoryException) { throw; }
            catch (Exception)
            {
                var empty = new AccountRangeMessage { RequestId = req.RequestId, Proof = new List<byte[]>() };
                return Task.FromResult(empty);
            }
        }

        private Task<AccountRangeMessage> GetAccountRangeCoreAsync(GetAccountRangeMessage req, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var effectiveBytes = Math.Min((ulong)SoftResponseLimit, req.ResponseBytes);
            var trie = PatriciaTrie.LoadFromStorage(req.RootHash, _stateNodes);
            var response = new AccountRangeMessage { RequestId = req.RequestId };

            byte[] lastKey = null;
            ulong size = 0;
            foreach (var entry in PatriciaRangeIterator.EnumerateRange(
                trie.Root, _stateNodes, req.StartingHash))
            {
                ct.ThrowIfCancellationRequested();
                var slim = SlimAccountEncoder.ToSlim(entry.Value);
                response.Accounts.Add(new AccountRangeMessage.AccountEntry
                {
                    Hash = entry.KeyBytes,
                    Body = slim
                });
                lastKey = entry.KeyBytes;
                size += (ulong)(32 + slim.Length);
                if (ByteArrayComparer.Current.Compare(entry.KeyBytes, req.LimitHash) >= 0) break;
                if (size > effectiveBytes) break;
            }

            if (response.Accounts.Count > 0)
            {
                response.Proof = PatriciaRangeProofGenerator.GenerateProof(
                    trie.Root, _stateNodes, req.StartingHash, lastKey);
            }
            else
            {
                response.Proof = PatriciaRangeProofGenerator.GenerateProof(
                    trie.Root, _stateNodes, req.StartingHash);
            }

            return Task.FromResult(response);
        }

        public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage req, CancellationToken ct = default)
        {
            try
            {
                return GetStorageRangesCoreAsync(req, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (OutOfMemoryException) { throw; }
            catch (Exception)
            {
                var empty = new StorageRangesMessage { RequestId = req.RequestId, Proof = new List<byte[]>() };
                return Task.FromResult(empty);
            }
        }

        private Task<StorageRangesMessage> GetStorageRangesCoreAsync(GetStorageRangesMessage req, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var response = new StorageRangesMessage { RequestId = req.RequestId };
            var initialOrigin = req.StartingHash.Length == 0 ? new byte[32] : req.StartingHash;
            var initialLimit = req.LimitHash.Length == 0 ? FilledHash(0xff) : req.LimitHash;

            var effectiveBytes = Math.Min((ulong)SoftResponseLimit, req.ResponseBytes);
            var hardLimit = (ulong)(effectiveBytes * (1.0 + StateLookupSlack));

            ulong size = 0;
            bool firstAccount = true;
            bool partialFromBudget = false;
            byte[] lastReturnedKey = null;
            byte[] storageRootForProof = null;
            byte[] startKeyForProof = null;

            foreach (var accountHash in req.AccountHashes)
            {
                ct.ThrowIfCancellationRequested();
                if (size >= effectiveBytes) break;

                var origin = firstAccount ? initialOrigin : new byte[32];
                var limit = firstAccount ? initialLimit : FilledHash(0xff);
                firstAccount = false;

                var storageRoot = ResolveStorageRoot(req.RootHash, accountHash);
                if (storageRoot == null || IsEmptyStorageRoot(storageRoot))
                {
                    response.Slots.Add(new List<StorageRangesMessage.SlotEntry>());
                    continue;
                }

                var storageTrie = PatriciaTrie.LoadFromStorage(storageRoot, _stateNodes);
                var perAccount = new List<StorageRangesMessage.SlotEntry>();
                bool brokeFromLimit = false;
                bool brokeFromBudget = false;

                foreach (var entry in PatriciaRangeIterator.EnumerateRange(storageTrie.Root, _stateNodes, origin))
                {
                    ct.ThrowIfCancellationRequested();
                    perAccount.Add(new StorageRangesMessage.SlotEntry
                    {
                        Hash = entry.KeyBytes,
                        Data = entry.Value
                    });
                    lastReturnedKey = entry.KeyBytes;
                    size += (ulong)(32 + entry.Value.Length);

                    if (ByteArrayComparer.Current.Compare(entry.KeyBytes, limit) >= 0)
                    {
                        brokeFromLimit = true;
                        break;
                    }
                    if (size > hardLimit)
                    {
                        brokeFromBudget = true;
                        partialFromBudget = true;
                        break;
                    }
                }

                response.Slots.Add(perAccount);

                if (brokeFromBudget || brokeFromLimit)
                {
                    storageRootForProof = storageRoot;
                    startKeyForProof = origin;
                }
                if (brokeFromBudget) break;
            }

            if (storageRootForProof != null && lastReturnedKey != null && partialFromBudget)
            {
                response.Proof = PatriciaRangeProofGenerator.GenerateProof(
                    PatriciaTrie.LoadFromStorage(storageRootForProof, _stateNodes).Root,
                    _stateNodes, startKeyForProof, lastReturnedKey);
            }
            else
            {
                response.Proof = new List<byte[]>();
            }
            return Task.FromResult(response);
        }

        private static byte[] FilledHash(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }

        public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage req, CancellationToken ct = default)
        {
            try
            {
                return GetByteCodesCoreAsync(req, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (OutOfMemoryException) { throw; }
            catch (Exception)
            {
                var empty = new ByteCodesMessage { RequestId = req.RequestId };
                return Task.FromResult(empty);
            }
        }

        private Task<ByteCodesMessage> GetByteCodesCoreAsync(GetByteCodesMessage req, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var response = new ByteCodesMessage { RequestId = req.RequestId };
            var effectiveBytes = Math.Min((ulong)SoftResponseLimit, req.ResponseBytes);
            long bytesRemaining = (long)effectiveBytes;
            foreach (var hash in req.Hashes)
            {
                ct.ThrowIfCancellationRequested();
                if (response.Codes.Count >= MaxCodeLookups) break;
                byte[] code;
                if (ByteUtil.AreEqual(hash, DefaultValues.EMPTY_DATA_HASH))
                {
                    code = new byte[0];
                }
                else
                {
                    code = _bytecodes.Get(hash);
                    if (code == null) continue;
                }
                response.Codes.Add(code);
                bytesRemaining -= code.Length;
                if (bytesRemaining <= 0) break;
            }
            return Task.FromResult(response);
        }

        public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage req, CancellationToken ct = default)
        {
            try
            {
                return GetTrieNodesCoreAsync(req, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (OutOfMemoryException) { throw; }
            catch (Exception)
            {
                var empty = new TrieNodesMessage { RequestId = req.RequestId };
                return Task.FromResult(empty);
            }
        }

        private Task<TrieNodesMessage> GetTrieNodesCoreAsync(GetTrieNodesMessage req, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var response = new TrieNodesMessage { RequestId = req.RequestId };

            foreach (var early in req.Paths)
                if (early.Count == 0) return Task.FromResult(response);

            var effectiveBytes = Math.Min((ulong)SoftResponseLimit, req.ResponseBytes);
            var accountTrie = PatriciaTrie.LoadFromStorage(req.RootHash, _stateNodes);
            ulong bytes = 0;
            var sw = Stopwatch.StartNew();

            foreach (var pathset in req.Paths)
            {
                ct.ThrowIfCancellationRequested();
                if (response.Nodes.Count >= MaxTrieNodeLookups) break;
                if (sw.Elapsed > MaxTrieNodeTimeSpent) break;

                if (pathset.Count == 1)
                {
                    var nibbles = PatriciaPathWalker.CompactToNibbles(pathset[0]);
                    var blob = PatriciaPathWalker.WalkPath(accountTrie.Root, _stateNodes, nibbles);
                    response.Nodes.Add(blob);
                    bytes += (ulong)blob.Length;
                    if (bytes > effectiveBytes) break;
                    continue;
                }

                var accountHash = pathset[0];
                if (accountHash.Length != 32) continue;
                var storageRoot = ResolveStorageRoot(req.RootHash, accountHash);
                if (storageRoot == null || IsEmptyStorageRoot(storageRoot)) continue;
                var storageTrie = PatriciaTrie.LoadFromStorage(storageRoot, _stateNodes);
                bool stop = false;
                for (int i = 1; i < pathset.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (response.Nodes.Count >= MaxTrieNodeLookups) { stop = true; break; }
                    if (sw.Elapsed > MaxTrieNodeTimeSpent) { stop = true; break; }
                    var nibbles = PatriciaPathWalker.CompactToNibbles(pathset[i]);
                    var blob = PatriciaPathWalker.WalkPath(storageTrie.Root, _stateNodes, nibbles);
                    response.Nodes.Add(blob);
                    bytes += (ulong)blob.Length;
                    if (bytes > effectiveBytes) { stop = true; break; }
                }
                if (stop) break;
                if (bytes > effectiveBytes) break;
            }
            return Task.FromResult(response);
        }

        private byte[] ResolveStorageRoot(byte[] stateRoot, byte[] accountHash)
        {
            if (_accountStateRoot != null)
            {
                var explicitRoot = _accountStateRoot(accountHash);
                if (explicitRoot != null) return explicitRoot;
            }
            var trie = PatriciaTrie.LoadFromStorage(stateRoot, _stateNodes);
            var body = trie.Get(accountHash, _stateNodes);
            if (body == null || body.Length == 0) return null;
            var account = new AccountEncoder().Decode(body);
            return account.StateRoot;
        }

        private static bool IsEmptyStorageRoot(byte[] root)
        {
            return root == null || ByteUtil.AreEqual(root, DefaultValues.EMPTY_TRIE_HASH);
        }
    }
}
