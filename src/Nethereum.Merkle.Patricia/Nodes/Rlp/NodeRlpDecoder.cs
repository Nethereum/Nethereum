using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
namespace Nethereum.Merkle.Patricia.Nodes.Rlp
{
    public class NodeRlpDecoder
    {
        private readonly IHashProvider _hashProvider;

        public NodeRlpDecoder() : this(Sha3KeccackHashProvider.Instance)
        {
        }

        public NodeRlpDecoder(IHashProvider hashProvider)
        {
            _hashProvider = hashProvider;
        }

        public static Node Decode(byte[] rlp, byte[] owner, byte[] path, IHashProvider hashProvider)
        {
            var node = new NodeRlpDecoder(hashProvider).DecodeFromRlpData(rlp, owner, path ?? new byte[0], false, null);
            node?.MarkPersisted();
            return node;
        }

        public Node Decode(HashNode reference, ITrieNodeStore store, bool decodeHashNodes)
        {
            var blob = store.Get(reference);
            if (blob == null || blob.Length == 0)
            {
                if (IsEmptyTrieRoot(reference.Hash)) return new EmptyNode(_hashProvider);
                throw new MissingTrieNodeException(reference.Owner, reference.Path, reference.Hash);
            }
            var node = DecodeFromRlpData(blob, reference.Owner, reference.Path ?? new byte[0], decodeHashNodes, store);
            node?.MarkPersisted();
            return node;
        }

        private bool IsEmptyTrieRoot(byte[] hash)
            => hash != null && hash.Length == 32
               && ByteUtil.AreEqual(hash, _hashProvider.ComputeHash(RLP.RLP.EncodeElement(new byte[0])));

        public Node DecodeFromRlpData(byte[] currentData, byte[] owner, byte[] path, bool decodeHashNodes, ITrieNodeStore store)
        {
            if (currentData == null || currentData.Length == 0) return new EmptyNode(_hashProvider);

            var decodedData = RLP.RLP.Decode(currentData);
            if (decodedData is RLPCollection decodedRlp)
            {
                if (decodedRlp.Count == 2)
                {
                    var shortNode = DecodeShortNode(decodedRlp, owner, path, decodeHashNodes, store);
                    if (shortNode != null) return shortNode;
                }
                if (decodedRlp.Count == 17)
                {
                    return DecodeBranchNode(decodedRlp, owner, path, decodeHashNodes, store);
                }
            }

            return null;
        }

        private Node DecodeShortNode(RLPCollection decodedRlp, byte[] owner, byte[] path, bool decodeHashNodes, ITrieNodeStore store)
        {
            var compactKey = decodedRlp[0].RLPData;
            var flag = compactKey[0] >> 4;
            if (flag == 2 || flag == 3)
            {
                var leafNode = new LeafNode(_hashProvider);
                leafNode.Nibbles = PatriciaPathWalker.CompactToNibbles(compactKey);
                leafNode.Value = decodedRlp[1].RLPData;
                leafNode.Owner = owner;
                leafNode.Path = path;
                return leafNode;
            }
            if (flag == 0 || flag == 1)
            {
                var extendedNode = new ExtendedNode(_hashProvider);
                extendedNode.Nibbles = PatriciaPathWalker.CompactToNibbles(compactKey);
                var childPath = path.ConcatArrays(extendedNode.Nibbles);
                extendedNode.InnerNode = DecodeChild(decodedRlp[1].RLPData, owner, childPath, decodeHashNodes, store);
                extendedNode.Owner = owner;
                extendedNode.Path = path;
                return extendedNode;
            }
            return null;
        }

        private Node DecodeBranchNode(RLPCollection decodedRlp, byte[] owner, byte[] path, bool decodeHashNodes, ITrieNodeStore store)
        {
            var branchNode = new BranchNode(_hashProvider);
            for (int i = 0; i < 16; i++)
            {
                var item = decodedRlp[i];
                if (item.RLPData != null && item.RLPData.Length != 0)
                {
                    var childPath = path.ConcatArrays(new byte[] { (byte)i });
                    branchNode.SetChild(i, DecodeChild(item.RLPData, owner, childPath, decodeHashNodes, store));
                }
            }
            if (decodedRlp[16] != null && decodedRlp[16].RLPData != null)
                branchNode.Value = decodedRlp[16].RLPData;
            branchNode.Owner = owner;
            branchNode.Path = path;
            return branchNode;
        }

        private Node DecodeChild(byte[] childRlpData, byte[] owner, byte[] childPath, bool decodeHashNodes, ITrieNodeStore store)
        {
            if (childRlpData.Length == 32)
            {
                var hashNode = new HashNode(_hashProvider) { Hash = childRlpData, Owner = owner, Path = childPath };
                if (decodeHashNodes)
                    hashNode.InnerNode = Decode(hashNode, store, decodeHashNodes);
                return hashNode;
            }
            return DecodeFromRlpData(childRlpData, owner, childPath, decodeHashNodes, store);
        }
    }
}
