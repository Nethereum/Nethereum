namespace Nethereum.Merkle.Patricia.Nodes.Rlp
{
    public static class EmptyNodeRlpSerializer
    {
        public static byte[] Encode(EmptyNode node)
        {
            return RLP.RLP.EncodeElement(new byte[0]);
        }
    }
}
