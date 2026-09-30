using Nethereum.Documentation;
using Nethereum.Merkle.Patricia.Nodes;
namespace Nethereum.Merkle.Patricia.Storage
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "key-path-storage", "The one node-store contract a PatriciaTrie holds")]
    public interface ITrieNodeStore
    {
        void Commit(TrieNodeSet nodes);
        byte[] Get(Node reference);
        bool Contains(Node reference);
        void Flush();
        void Clear();

        bool ContainsKey(byte[] stateRoot);
    }
}
