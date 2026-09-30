namespace Nethereum.Merkle.Patricia.Nodes.Rlp
{
    public static class HashNodeRlpSerializer
    {
        public static byte[] Encode(HashNode node)
        {
            if (node.InnerNode is EmptyNode) return node.Hash;
            if (node.InnerNode != null) return node.InnerNode.GetEncodedData();
            return node.Hash;
        }
    }
}
