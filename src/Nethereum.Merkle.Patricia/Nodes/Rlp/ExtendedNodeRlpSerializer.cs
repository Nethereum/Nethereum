using System.Collections.Generic;

namespace Nethereum.Merkle.Patricia.Nodes.Rlp
{
    public static class ExtendedNodeRlpSerializer
    {
        public static byte[] Encode(ExtendedNode node)
        {
            var returnByteArray = new List<byte[]>();
            var nibblesByteArray = node.GetPrefixedNibbles().ConvertFromNibbles();
            returnByteArray.Add(RLP.RLP.EncodeElement(nibblesByteArray));
            var encodedDataNextNode = node.InnerNode.GetEncodedData();
            if (encodedDataNextNode.Length >= 32)
            {
                returnByteArray.Add(RLP.RLP.EncodeElement(node.InnerNode.GetHash()));
            }
            else
            {
                returnByteArray.Add(encodedDataNextNode);
            }
            return RLP.RLP.EncodeList(returnByteArray.ToArray());
        }
    }
}
