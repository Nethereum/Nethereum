using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Merkle.Patricia
{
    public class NodeDecoder
    {
        public Node DecodeNode(byte[] hash, bool decodeHashNodes, ITrieStorage storage)
        {
            var currentData = storage.Get(hash);
            return DecodeNodeFromRlpData(currentData, decodeHashNodes, storage);
        }

        public Node DecodeNodeFromRlpData(byte[] currentData, bool decodeHashNodes, ITrieStorage storage)
        {
            if (currentData == null || currentData.Length == 0) return new EmptyNode();
            if (currentData.Length == 32)
            {
                if (decodeHashNodes)
                {
                    return new HashNode()
                    {
                        Hash = currentData,
                        InnerNode = DecodeNode(currentData, decodeHashNodes, storage)
                    };
                }
                else
                {
                    return new HashNode()
                    {
                        Hash = currentData
                    };
                }
            }
            var decodedData = RLP.RLP.Decode(currentData);
            if (decodedData is RLPCollection decodedRlp)
            {
                if (decodedRlp.Count == 2)
                {
                    var keyAsNibbles = decodedRlp[0].RLPData.ConvertToNibbles();
                    if (keyAsNibbles[0] == 2 || keyAsNibbles[0] == 3)
                    {
                        var leafNode = new LeafNode();
                        if (keyAsNibbles[0] == 2)
                        {
                            leafNode.Nibbles = keyAsNibbles.SliceFrom(2);
                        }
                        else
                        {
                            leafNode.Nibbles = keyAsNibbles.SliceFrom(1);
                        }
                        leafNode.Value = decodedRlp[1].RLPData;
                        return leafNode;
                    }

                    if (keyAsNibbles[0] == 0 || keyAsNibbles[0] == 1)
                    {

                        var extendedNode = new ExtendedNode();
                        if (keyAsNibbles[0] == 0)
                        {
                            extendedNode.Nibbles = keyAsNibbles.SliceFrom(2);
                        }
                        else
                        {
                            extendedNode.Nibbles = keyAsNibbles.SliceFrom(1);
                        }
                        extendedNode.InnerNode = DecodeNodeFromRlpData(decodedRlp[1].RLPData, decodeHashNodes, storage);
                        return extendedNode;
                    }

                }
                if (decodedRlp.Count == 17)
                {
                    return DecodeBranchNode(decodedRlp, decodeHashNodes, storage);
                }
            }

            return null;
        }

        public BranchNode DecodeBranchNode(RLPCollection rlpCollection, bool decodeHashNodes, ITrieStorage storage)
        {
            var branchNode = new BranchNode();

            for (int i = 0; i < 16; i++)
            {
                var item = rlpCollection[i];

                if (item.RLPData != null && item.RLPData.Length != 0)
                {
                    branchNode.SetChild(i, DecodeNodeFromRlpData(item.RLPData, decodeHashNodes, storage));
                }
                else
                {
                    //empty node
                }
            }

            if (rlpCollection[16] != null && rlpCollection[16].RLPData != null)
            {
                branchNode.Value = rlpCollection[16].RLPData;
            }

            return branchNode;
        }

    }
}
