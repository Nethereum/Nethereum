using System;
using System.Collections.Generic;
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
    /// Same machinery serves three things at once:
    /// - go-ethereum's `devp2p rlpx snap-test` conformance suite (5 sub-tests)
    /// - AppChain fast-follower state bootstrap (sync state without re-executing all blocks)
    /// - Indexer cold start (walk state at a specific anchored block root)
    ///
    /// Trust model for AppChain followers: the receiver checks the resulting
    /// state root against the L1 anchor record before accepting the synced state.
    /// </summary>
    public class PatriciaSnapRequestHandler : ISnapRequestHandler
    {
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
            // Defensive: a peer may request an arbitrary root that isn't a
            // state trie (it could be a storage root, a random hash, or just
            // unknown). The trie load may succeed but iteration will then hit
            // values that aren't decodable as accounts. Return an empty
            // response in any such case rather than tearing down the snap
            // session — go-ethereum's serveSnapReq exhibits the same behaviour.
            try
            {
                return GetAccountRangeCoreAsync(req, ct);
            }
            catch
            {
                var empty = new AccountRangeMessage { RequestId = req.RequestId, Proof = new List<byte[]>() };
                return Task.FromResult(empty);
            }
        }

        private Task<AccountRangeMessage> GetAccountRangeCoreAsync(GetAccountRangeMessage req, CancellationToken ct)
        {
            var trie = PatriciaTrie.LoadFromStorage(req.RootHash, _stateNodes);
            var response = new AccountRangeMessage { RequestId = req.RequestId };

            byte[] lastKey = null;
            // Match Geth's serveSnapReq semantics exactly (handler.go ~L304):
            //   for each entry:
            //     append
            //     size += 32 + len(slim)
            //     if hash >= LimitHash: break          (limit is literal, even 0)
            //     if size > Bytes: break               (allows first to exceed budget)
            // This yields at least one account even with budget=0 or budget=1,
            // and follows the inclusive-of-overshoot bound on Limit.
            ulong size = 0;
            foreach (var entry in PatriciaRangeIterator.EnumerateRange(
                trie.Root, _stateNodes, req.StartingHash))
            {
                var slim = SlimAccountEncoder.ToSlim(entry.Value);
                response.Accounts.Add(new AccountRangeMessage.AccountEntry
                {
                    Hash = entry.KeyBytes,
                    Body = slim
                });
                lastKey = entry.KeyBytes;
                size += (ulong)(32 + slim.Length);
                if (ByteArrayComparer.Current.Compare(entry.KeyBytes, req.LimitHash) >= 0) break;
                if (size > req.ResponseBytes) break;
            }

            if (response.Accounts.Count > 0)
            {
                response.Proof = PatriciaRangeProofGenerator.GenerateProof(
                    trie.Root, _stateNodes, req.StartingHash, lastKey);
            }
            else
            {
                // Spec: empty range still needs a proof of non-existence so the
                // receiver can verify the trie is genuinely empty in this slice.
                response.Proof = PatriciaRangeProofGenerator.GenerateProof(
                    trie.Root, _stateNodes, req.StartingHash);
            }

            return Task.FromResult(response);
        }

        public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage req, CancellationToken ct = default)
        {
            // snap/1 GetStorageRanges semantics:
            // The first account uses req.Origin and req.Limit; subsequent
            // accounts use zero..max. For each account: append slot, size +=
            // 32 + len(value), break if slot >= limit (append-then-check), or
            // if size > Bytes. Only the LAST partial range gets an edge proof.
            var response = new StorageRangesMessage { RequestId = req.RequestId };
            var initialOrigin = req.StartingHash.Length == 0 ? new byte[32] : req.StartingHash;
            var initialLimit = req.LimitHash.Length == 0 ? FilledHash(0xff) : req.LimitHash;

            ulong size = 0;
            bool firstAccount = true;
            bool partialFromBudget = false;
            byte[] lastReturnedKey = null;
            byte[] storageRootForProof = null;
            byte[] startKeyForProof = null;

            foreach (var accountHash in req.AccountHashes)
            {
                if (size >= req.ResponseBytes) break;

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
                    if (size > req.ResponseBytes)
                    {
                        brokeFromBudget = true;
                        partialFromBudget = true;
                        break;
                    }
                }

                response.Slots.Add(perAccount);

                // Per spec, a proof is required only when the response covers
                // a partial range — i.e. we stopped at the byte budget, OR we
                // stopped at the requested Limit on the LAST account in the
                // accounts list (because the caller might want to continue).
                if (brokeFromBudget || brokeFromLimit)
                {
                    storageRootForProof = storageRoot;
                    startKeyForProof = origin;
                }
                if (brokeFromBudget) break;
            }

            // Single edge-proof bundle attached to the response when the LAST
            // returned account had a partial range — exactly how Geth's
            // handler.ServiceGetStorageRangesQuery builds proofs.
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
            // Per Geth's chain.ContractCodeWithPrefix + handler:
            //   - EMPTY_DATA_HASH (keccak256("")) returns an empty code (deliver it)
            //   - Stored hashes return their code
            //   - Unknown hashes (random / state roots) are nil → skip
            // The receiver maps returned codes back to requested hashes by
            // re-hashing each entry; correct sequencing matters but we must
            // also follow the byte budget (deliver at least one even if
            // bytes==0 or 1).
            var response = new ByteCodesMessage { RequestId = req.RequestId };
            long bytesRemaining = (long)req.ResponseBytes;
            foreach (var hash in req.Hashes)
            {
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
            // snap/1 GetTrieNodes semantics:
            //   - Single-element pathset: walk the account trie at compact-hex
            //     path; append node RLP (or empty bytes if path diverges).
            //   - Multi-element pathset: pathset[0] = 32-byte account-hash,
            //     load that account's storage root, then for each subsequent
            //     path walk the storage trie and append. The accountNode
            //     itself is NOT appended in multi-element form.
            //   - Zero-element pathset is a protocol error; we silently skip
            //     (matches a returned empty response).
            var response = new TrieNodesMessage { RequestId = req.RequestId };

            // Geth's handler returns an error (which disconnects the peer)
            // on a zero-item pathset — we just emit an empty response, which
            // surfaces to Geth's conformance test as "trienode count 0".
            foreach (var early in req.Paths)
                if (early.Count == 0) return Task.FromResult(response);

            var accountTrie = PatriciaTrie.LoadFromStorage(req.RootHash, _stateNodes);
            ulong bytes = 0;

            foreach (var pathset in req.Paths)
            {

                if (pathset.Count == 1)
                {
                    var nibbles = PatriciaPathWalker.CompactToNibbles(pathset[0]);
                    var blob = PatriciaPathWalker.WalkPath(accountTrie.Root, _stateNodes, nibbles);
                    response.Nodes.Add(blob);
                    bytes += (ulong)blob.Length;
                    if (bytes > req.ResponseBytes) break;
                    continue;
                }

                // Multi-element: open storage trie of the account, walk each
                // storage path from there.
                var accountHash = pathset[0];
                if (accountHash.Length != 32) continue;
                var storageRoot = ResolveStorageRoot(req.RootHash, accountHash);
                if (storageRoot == null || IsEmptyStorageRoot(storageRoot)) continue;
                var storageTrie = PatriciaTrie.LoadFromStorage(storageRoot, _stateNodes);
                for (int i = 1; i < pathset.Count; i++)
                {
                    var nibbles = PatriciaPathWalker.CompactToNibbles(pathset[i]);
                    var blob = PatriciaPathWalker.WalkPath(storageTrie.Root, _stateNodes, nibbles);
                    response.Nodes.Add(blob);
                    bytes += (ulong)blob.Length;
                    if (bytes > req.ResponseBytes) break;
                }
                if (bytes > req.ResponseBytes) break;
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
            // Fallback: look up the account body in the state trie and decode it.
            var trie = PatriciaTrie.LoadFromStorage(stateRoot, _stateNodes);
            var body = trie.Get(accountHash, _stateNodes);
            if (body == null || body.Length == 0) return null;
            var account = new AccountEncoder().Decode(body);
            return account.StateRoot;
        }

        private byte[] ResolveStorageRootFromPath(byte[] stateRoot, byte[] accountPath)
        {
            // Only handle the common case: accountPath = 32-byte full hash key.
            if (accountPath == null || accountPath.Length != 32) return null;
            return ResolveStorageRoot(stateRoot, accountPath);
        }

        private byte[] ResolveTrieNodeAtPath(byte[] rootHash, byte[] path)
        {
            // Path encoding from the snap spec is the compact hex prefix used
            // by Patricia nodes. For the common case of a 32-byte full key
            // (account-hash or slot-hash), we walk the trie to the leaf.
            if (path == null) return null;
            if (path.Length == 32)
            {
                var trie = PatriciaTrie.LoadFromStorage(rootHash, _stateNodes);
                var proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, _stateNodes, path);
                if (proof.Count == 0) return null;
                return proof[proof.Count - 1];
            }
            // Non-32-byte paths (partial paths) — not supported in this minimal
            // handler; snap-test does exercise these.
            return null;
        }

        private static bool IsZeroHash(byte[] h)
        {
            if (h == null || h.Length == 0) return true;
            for (int i = 0; i < h.Length; i++) if (h[i] != 0) return false;
            return true;
        }

        private static bool IsEmptyStorageRoot(byte[] root)
        {
            // keccak256(rlp("")) — the canonical empty-trie root used when an
            // account has no storage.
            return root == null || ByteUtil.AreEqual(root, DefaultValues.EMPTY_TRIE_HASH);
        }
    }
}
