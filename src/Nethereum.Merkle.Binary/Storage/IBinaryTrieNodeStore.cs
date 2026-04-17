using System.Collections.Generic;

namespace Nethereum.Merkle.Binary.Storage
{
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
