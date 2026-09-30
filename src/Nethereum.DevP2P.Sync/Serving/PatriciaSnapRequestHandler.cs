using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.CoreChain.Storage;

namespace Nethereum.DevP2P.Sync.Serving
{
    public class PatriciaSnapRequestHandler : ISnapRequestHandler
    {
        public const int SoftResponseLimit = 2 * 1024 * 1024;

        public const int MaxCodeLookups = 1024;

        public const int MaxTrieNodeLookups = 1024;

        public const int MaxBlockAccessListLookups = 1024;

        public static readonly TimeSpan MaxTrieNodeTimeSpent = TimeSpan.FromSeconds(5);

        public const double StateLookupSlack = 0.1;

        private readonly ITrieNodeStore _nodeStore;
        private readonly IBytecodeStore _bytecodes;
        private readonly Func<byte[], byte[]> _accountStateRoot;
        private readonly int _softResponseLimit;
        private readonly ISnapNodeStoreSelector _selector;
        private readonly IBlockAccessListStore _blockAccessLists;

        public PatriciaSnapRequestHandler(
            ITrieNodeStore stateNodeStore,
            IBytecodeStore bytecodeStore,
            Func<byte[], byte[]> accountStateRootProvider = null,
            int softResponseLimit = SoftResponseLimit,
            ISnapNodeStoreSelector selector = null,
            IBlockAccessListStore blockAccessLists = null)
        {
            if (stateNodeStore == null) throw new ArgumentNullException(nameof(stateNodeStore));
            _nodeStore = stateNodeStore;
            _bytecodes = bytecodeStore ?? throw new ArgumentNullException(nameof(bytecodeStore));
            _accountStateRoot = accountStateRootProvider;
            _softResponseLimit = softResponseLimit;
            _selector = selector;
            _blockAccessLists = blockAccessLists;
        }

        private static async Task<TResponse> SafeServeAsync<TResponse>(Func<Task<TResponse>> core, Func<TResponse> emptyOnError)
        {
            try
            {
                return await core();
            }
            catch (OperationCanceledException) { throw; }
            catch (OutOfMemoryException) { throw; }
            catch (SnapMalformedRequestException) { throw; }
            catch (Exception)
            {
                return emptyOnError();
            }
        }

        public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage req, CancellationToken ct = default)
            => SafeServeAsync(
                () => GetAccountRangeCoreAsync(req, ct),
                () => new AccountRangeMessage { RequestId = req.RequestId, Proof = new List<byte[]>() });

        private async Task<AccountRangeMessage> GetAccountRangeCoreAsync(GetAccountRangeMessage req, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var store = _selector != null ? (await _selector.ResolveForRootAsync(req.RootHash, ct)) ?? _nodeStore : _nodeStore;
            var effectiveBytes = Math.Min((ulong)_softResponseLimit, req.ResponseBytes);
            var trie = PatriciaTrie.LoadFromStorage(req.RootHash, store);
            var response = new AccountRangeMessage { RequestId = req.RequestId };

            byte[] lastKey = null;
            ulong size = 0;
            foreach (var entry in PatriciaRangeIterator.EnumerateRange(
                trie.Root, store, req.StartingHash))
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
                    trie.Root, store, req.StartingHash, lastKey);
            }
            else
            {
                response.Proof = PatriciaRangeProofGenerator.GenerateProof(
                    trie.Root, store, req.StartingHash);
            }

            return response;
        }

        public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage req, CancellationToken ct = default)
            => SafeServeAsync(
                () => GetStorageRangesCoreAsync(req, ct),
                () => new StorageRangesMessage { RequestId = req.RequestId, Proof = new List<byte[]>() });

        private async Task<StorageRangesMessage> GetStorageRangesCoreAsync(GetStorageRangesMessage req, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var store = _selector != null ? (await _selector.ResolveForRootAsync(req.RootHash, ct)) ?? _nodeStore : _nodeStore;
            var response = new StorageRangesMessage { RequestId = req.RequestId };
            var initialOrigin = req.StartingHash.Length == 0 ? new byte[32] : req.StartingHash;
            var initialLimit = req.LimitHash.Length == 0 ? FilledHash(0xff) : req.LimitHash;

            var effectiveBytes = Math.Min((ulong)_softResponseLimit, req.ResponseBytes);
            var hardLimit = (ulong)(effectiveBytes * (1.0 + StateLookupSlack));

            ulong size = 0;
            bool firstAccount = true;
            byte[] lastReturnedKey = null;
            byte[] storageRootForProof = null;
            byte[] accountHashForProof = null;
            byte[] startKeyForProof = null;
            byte[] lastKeyForProof = null;

            foreach (var accountHash in req.AccountHashes)
            {
                ct.ThrowIfCancellationRequested();
                if (size >= effectiveBytes) break;

                var origin = firstAccount ? initialOrigin : new byte[32];
                var limit = firstAccount ? initialLimit : FilledHash(0xff);
                firstAccount = false;

                var storageRoot = ResolveStorageRoot(req.RootHash, accountHash, store);
                if (storageRoot == null || IsEmptyStorageRoot(storageRoot))
                {
                    response.Slots.Add(new List<StorageRangesMessage.SlotEntry>());
                    continue;
                }

                var storageTrie = PatriciaTrie.LoadFromStorage(storageRoot, store, accountHash);
                var perAccount = new List<StorageRangesMessage.SlotEntry>();
                bool brokeFromLimit = false;
                bool brokeFromBudget = false;

                foreach (var entry in PatriciaRangeIterator.EnumerateRange(storageTrie.Root, store, origin))
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
                        break;
                    }
                }

                response.Slots.Add(perAccount);

                var needsProof = !ByteUtil.IsZero(origin) || brokeFromLimit || brokeFromBudget;
                if (needsProof)
                {
                    storageRootForProof = storageRoot;
                    accountHashForProof = accountHash;
                    startKeyForProof = origin;
                    lastKeyForProof = perAccount.Count > 0 ? lastReturnedKey : null;
                    break;
                }
            }

            if (storageRootForProof != null)
            {
                var proofRoot = PatriciaTrie.LoadFromStorage(storageRootForProof, store, accountHashForProof).Root;
                response.Proof = lastKeyForProof != null
                    ? PatriciaRangeProofGenerator.GenerateProof(proofRoot, store, startKeyForProof, lastKeyForProof)
                    : PatriciaRangeProofGenerator.GenerateProof(proofRoot, store, startKeyForProof);
            }
            else
            {
                response.Proof = new List<byte[]>();
            }
            return response;
        }

        private static byte[] FilledHash(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }

        public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage req, CancellationToken ct = default)
            => SafeServeAsync(
                () => GetByteCodesCoreAsync(req, ct),
                () => new ByteCodesMessage { RequestId = req.RequestId });

        private Task<ByteCodesMessage> GetByteCodesCoreAsync(GetByteCodesMessage req, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var response = new ByteCodesMessage { RequestId = req.RequestId };
            var effectiveBytes = Math.Min((ulong)_softResponseLimit, req.ResponseBytes);
            long bytesRemaining = (long)effectiveBytes;

            var hashes = req.Hashes;
            var lookupLimit = hashes.Count < MaxCodeLookups ? hashes.Count : MaxCodeLookups;
            for (int i = 0; i < lookupLimit; i++)
            {
                ct.ThrowIfCancellationRequested();
                var hash = hashes[i];
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
            => SafeServeAsync(
                () => GetTrieNodesCoreAsync(req, ct),
                () => new TrieNodesMessage { RequestId = req.RequestId });

        private async Task<TrieNodesMessage> GetTrieNodesCoreAsync(GetTrieNodesMessage req, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var store = _selector != null ? (await _selector.ResolveForRootAsync(req.RootHash, ct)) ?? _nodeStore : _nodeStore;
            var response = new TrieNodesMessage { RequestId = req.RequestId };

            foreach (var early in req.Paths)
                if (early.Count == 0)
                    throw new SnapMalformedRequestException(
                        $"GetTrieNodes request {req.RequestId}: zero-item pathset (malformed request)");

            var effectiveBytes = Math.Min((ulong)_softResponseLimit, req.ResponseBytes);
            var accountTrie = PatriciaTrie.LoadFromStorage(req.RootHash, store);
            ulong bytes = 0;
            var sw = Stopwatch.StartNew();

            int attempts = 0;

            foreach (var pathset in req.Paths)
            {
                ct.ThrowIfCancellationRequested();
                if (attempts >= MaxTrieNodeLookups) break;
                if (sw.Elapsed > MaxTrieNodeTimeSpent) break;

                if (pathset.Count == 1)
                {
                    attempts++;
                    var nibbles = PatriciaPathWalker.CompactToNibbles(pathset[0]);
                    var blob = PatriciaPathWalker.WalkPath(accountTrie.Root, store, nibbles);
                    response.Nodes.Add(blob);
                    bytes += (ulong)blob.Length;
                    if (bytes > effectiveBytes) break;
                    continue;
                }

                var accountHash = pathset[0];
                if (accountHash.Length != 32) continue;
                attempts++;
                var storageRoot = ResolveStorageRoot(req.RootHash, accountHash, store);
                if (storageRoot == null || IsEmptyStorageRoot(storageRoot)) continue;
                var storageTrie = PatriciaTrie.LoadFromStorage(storageRoot, store, accountHash);
                bool stop = false;
                for (int i = 1; i < pathset.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (attempts >= MaxTrieNodeLookups) { stop = true; break; }
                    if (sw.Elapsed > MaxTrieNodeTimeSpent) { stop = true; break; }
                    attempts++;
                    var nibbles = PatriciaPathWalker.CompactToNibbles(pathset[i]);
                    var blob = PatriciaPathWalker.WalkPath(storageTrie.Root, store, nibbles);
                    response.Nodes.Add(blob);
                    bytes += (ulong)blob.Length;
                    if (bytes > effectiveBytes) { stop = true; break; }
                }
                if (stop) break;
                if (bytes > effectiveBytes) break;
            }
            return response;
        }

        public Task<BlockAccessListsMessage> GetBlockAccessListsAsync(GetBlockAccessListsMessage req, CancellationToken ct = default)
            => SafeServeAsync(
                () => GetBlockAccessListsCoreAsync(req, ct),
                () => new BlockAccessListsMessage { RequestId = req.RequestId });

        private async Task<BlockAccessListsMessage> GetBlockAccessListsCoreAsync(GetBlockAccessListsMessage req, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var response = new BlockAccessListsMessage { RequestId = req.RequestId };
            var effectiveBytes = Math.Min((ulong)_softResponseLimit, req.Bytes);

            var hashes = req.Hashes;
            var lookupLimit = hashes.Count < MaxBlockAccessListLookups ? hashes.Count : MaxBlockAccessListLookups;
            ulong bytesUsed = 0;
            for (int i = 0; i < lookupLimit; i++)
            {
                ct.ThrowIfCancellationRequested();

                var entry = await BlockAccessListOrEmptyAsync(hashes[i]).ConfigureAwait(false);
                response.BlockAccessListsByBlock.Add(entry);
                bytesUsed += (ulong)entry.Length;
                if (bytesUsed > effectiveBytes) break;
            }
            return response;
        }

        private async Task<byte[]> BlockAccessListOrEmptyAsync(byte[] blockHash)
        {
            if (_blockAccessLists == null || blockHash == null || blockHash.Length != 32)
                return Array.Empty<byte>();

            return await _blockAccessLists.GetByBlockHashAsync(blockHash).ConfigureAwait(false)
                   ?? Array.Empty<byte>();
        }

        public sealed class SnapMalformedRequestException : Exception
        {
            public SnapMalformedRequestException(string message) : base(message) { }
        }

        private byte[] ResolveStorageRoot(byte[] stateRoot, byte[] accountHash, ITrieNodeStore store)
        {
            if (_accountStateRoot != null)
            {
                var explicitRoot = _accountStateRoot(accountHash);
                if (explicitRoot != null) return explicitRoot;
            }
            var trie = PatriciaTrie.LoadFromStorage(stateRoot, store);
            var body = trie.Get(accountHash);
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
