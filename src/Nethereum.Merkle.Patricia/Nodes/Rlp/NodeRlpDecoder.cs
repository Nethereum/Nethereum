using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

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
            if (blob == null || blob.Length == 0) return new EmptyNode(_hashProvider);
            var node = DecodeFromRlpData(blob, reference.Owner, reference.Path ?? new byte[0], decodeHashNodes, store);
            node?.MarkPersisted();
            return node;
        }

        public Node DecodeFromRlpData(byte[] currentData, byte[] owner, byte[] path, bool decodeHashNodes, ITrieNodeStore store)
        {
            if (currentData == null || currentData.Length == 0) return new EmptyNode(_hashProvider);

            var decodedData = RLP.RLP.Decode(currentData);
            if (decodedData is RLPCollection decodedRlp)
            {
                if (decodedRlp.Count == 2)
                {
                    var keyAsNibbles = decodedRlp[0].RLPData.ConvertToNibbles();
                    if (keyAsNibbles[0] == 2 || keyAsNibbles[0] == 3)
                    {
                        var leafNode = new LeafNode(_hashProvider);
                        leafNode.Nibbles = keyAsNibbles.SliceFrom(keyAsNibbles[0] == 2 ? 2 : 1);
                        leafNode.Value = decodedRlp[1].RLPData;
                        leafNode.Owner = owner;
                        leafNode.Path = path;
                        return leafNode;
                    }
                    if (keyAsNibbles[0] == 0 || keyAsNibbles[0] == 1)
                    {
                        var extendedNode = new ExtendedNode(_hashProvider);
                        extendedNode.Nibbles = keyAsNibbles.SliceFrom(keyAsNibbles[0] == 0 ? 2 : 1);
                        var childPath = path.ConcatArrays(extendedNode.Nibbles);
                        extendedNode.InnerNode = DecodeChild(decodedRlp[1].RLPData, owner, childPath, decodeHashNodes, store);
                        extendedNode.Owner = owner;
                        extendedNode.Path = path;
                        return extendedNode;
                    }
                }
                if (decodedRlp.Count == 17)
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
            }

            return null;
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
