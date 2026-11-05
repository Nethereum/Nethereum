using System;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal enum RangeBoundary { Ref, Empty, NoBoundary, Missing }

    internal sealed class TrieProof
    {
        private readonly ITrieNodeStore _trieNodes;

        public TrieProof(ITrieNodeStore trieNodes)
        {
            _trieNodes = trieNodes;
        }

        internal (RangeBoundary Kind, byte[] Reference) VerifiedReference(byte[] owner, byte[] path, byte[] root)
        {
            if (ByteUtil.AreEqual(root, DefaultValues.EMPTY_TRIE_HASH)) return (RangeBoundary.Empty, null);
            var reference = root;
            var depth = 0;
            while (depth < path.Length)
            {
                var node = VerifiedNode(_trieNodes, owner, path.AsSpan(0, depth).ToArray(), reference);
                if (node == null) return (RangeBoundary.Missing, null);
                var (next, consumed, boundary) = Descend(node, path, depth);
                if (boundary != RangeBoundary.Ref) return (boundary, null);
                reference = next;
                depth += consumed;
                if (reference == null) return (RangeBoundary.Empty, null);
                if (depth < path.Length && reference.Length < 32) return (RangeBoundary.NoBoundary, null);
            }
            return (RangeBoundary.Ref, reference);
        }

        internal static (byte[] Child, int Consumed, RangeBoundary Boundary) Descend(Nethereum.RLP.RLPCollection node, byte[] path, int depth)
        {
            if (node.Count == 17) return (ChildReference(node[path[depth]]), 1, RangeBoundary.Ref);
            var key = PatriciaPathWalker.CompactToNibbles(node[0].RLPData);
            var remaining = path.Length - depth;
            for (int i = 0; i < Math.Min(key.Length, remaining); i++)
                if (key[i] != path[depth + i]) return (null, 0, RangeBoundary.Empty);
            if (key.Length > remaining || IsLeafNode(node)) return (null, key.Length, RangeBoundary.NoBoundary);
            return (ChildReference(node[1]), key.Length, RangeBoundary.Ref);
        }

        internal Nethereum.RLP.RLPCollection VerifiedNode(ITrieNodeStore store, byte[] owner, byte[] path, byte[] reference)
        {
            if (reference == null) return null;
            var blob = reference.Length < 32 ? reference : TryGet(store, new HashNode { Hash = reference, Owner = owner, Path = path });
            if (!IsAuthentic(blob, reference)) return null;
            try
            {
                return Nethereum.RLP.RLP.Decode(blob) is Nethereum.RLP.RLPCollection { Count: 2 or 17 } node ? node : null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return null;
            }
        }

        internal static byte[] TryGet(ITrieNodeStore store, Node reference)
        {
            try
            {
                return store.Get(reference);
            }
            catch (InvalidOperationException ex) when (ex is not ObjectDisposedException)
            {
                return null;
            }
        }

        internal static bool IsAuthentic(byte[] blob, byte[] reference)
            => blob is { Length: > 0 }
               && LooksLikeTrieNode(blob)
               && ProofVerification.Current.TrieNode.Verify(reference, blob, Sha3KeccackHashProvider.Instance);

        private static bool LooksLikeTrieNode(byte[] blob) => blob[0] >= 0xC0;

        internal static bool IsLeafNode(Nethereum.RLP.RLPCollection node)
            => node.Count == 2 && node[0].RLPData is { Length: > 0 } compact && (compact[0] >> 4 & 2) != 0;

        internal static byte[] ChildReference(Nethereum.RLP.IRLPElement element)
            => element is Nethereum.RLP.RLPCollection embedded ? embedded.RLPData
                : element.RLPData is { Length: > 0 } reference ? reference : null;

        internal static bool Proven((RangeBoundary Kind, byte[] Reference) verdict, RangeHash hashed)
            => !hashed.Malformed
               && ((verdict.Kind == RangeBoundary.Ref && ByteUtil.AreEqual(hashed.Reference, verdict.Reference))
                   || (verdict.Kind == RangeBoundary.Empty && hashed.Count == 0));
    }
}
