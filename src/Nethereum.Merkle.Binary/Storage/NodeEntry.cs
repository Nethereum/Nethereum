namespace Nethereum.Merkle.Binary.Storage
{
    public class NodeEntry
    {
        public byte[] Hash { get; set; }
        public byte[] Encoded { get; set; }
        public int Depth { get; set; }
        public byte NodeType { get; set; }
        public byte[] Stem { get; set; }
        public long BlockNumber { get; set; }
        public bool IsDirty { get; set; }
    }
}
