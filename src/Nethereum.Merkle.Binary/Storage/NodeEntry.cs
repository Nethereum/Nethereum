
using Nethereum.Documentation;

namespace Nethereum.Merkle.Binary.Storage
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "binary-trie-storage", "One stored binary-trie node with its metadata")]
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
