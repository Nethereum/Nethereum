using System.Collections.Generic;

namespace Nethereum.Merkle.Patricia.Nodes.Rlp
{
    public static class LeafNodeRlpSerializer
    {
        public static byte[] Encode(LeafNode node)
        {
            var returnByteArray = new List<byte[]>();
            var nibblesByteArray = node.GetPrefixedNibbles().ConvertFromNibbles();
            returnByteArray.Add(RLP.RLP.EncodeElement(nibblesByteArray));
            returnByteArray.Add(RLP.RLP.EncodeElement(node.Value));
            return RLP.RLP.EncodeList(returnByteArray.ToArray());
        }
    }
}
