using System.Collections.Generic;

namespace Nethereum.Merkle.Patricia.Nodes.Rlp
{
    public static class BranchNodeRlpSerializer
    {
        public static byte[] Encode(BranchNode node)
        {
            var returnByteArray = new List<byte[]>();
            foreach (Node child in node.Children)
            {
                if (child is null)
                {
                    returnByteArray.Add(EmptyNode.Instance.GetEncodedData());
                }
                else
                {
                    var encodedDataNextNode = child.GetEncodedData();
                    if (encodedDataNextNode.Length >= 32)
                    {
                        returnByteArray.Add(RLP.RLP.EncodeElement(child.GetHash()));
                    }
                    else
                    {
                        returnByteArray.Add(encodedDataNextNode);
                    }
                }
            }
            returnByteArray.Add(RLP.RLP.EncodeElement(node.Value));
            return RLP.RLP.EncodeList(returnByteArray.ToArray());
        }
    }
}
