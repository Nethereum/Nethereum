using Nethereum.Documentation;
using System.Collections.Generic;

namespace Nethereum.Merkle.Binary.Storage
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "binary-trie-storage", "The node store that indexes by depth, address and dirtiness")]
    public interface IBinaryTrieNodeStore : IBinaryTrieStorage
    {
        void PutNode(byte[] hash, byte[] encoded, int depth, byte nodeType, byte[] stem);

        void RegisterAddressStem(byte[] address, byte[] stemNodeHash);

        IReadOnlyList<NodeEntry> GetNodesByDepthRange(int minDepth, int maxDepth);

        IReadOnlyList<NodeEntry> GetStemNodesByAddress(byte[] address);

        IReadOnlyList<NodeEntry> GetDirtyNodes();

        void MarkBlockCommitted(long blockNumber);

        void ClearDirtyTracking();

        byte[] ExportCheckpoint(int maxDepth);

        void ImportCheckpoint(byte[] checkpoint);

        int NodeCount { get; }
    }
}
