using Nethereum.Merkle.Patricia.Nodes;
namespace Nethereum.Merkle.Patricia.Storage
{
    public class ContentAddressedNodeStore : ITrieNodeStore
    {
        private readonly INodeBlobStore _inner;

        public ContentAddressedNodeStore(INodeBlobStore inner)
        {
            _inner = inner;
        }

        public static ITrieNodeStore Wrap(INodeBlobStore storage)
            => storage as ITrieNodeStore ?? new ContentAddressedNodeStore(storage);

        public void Commit(TrieNodeSet nodes)
        {
            if (nodes == null) return;
            foreach (var node in nodes.Nodes)
                _inner.Put(node.GetHash(), node.GetEncodedData());
        }

        public byte[] Get(Node reference) => _inner.Get(reference.GetHash());

        public bool Contains(Node reference) => _inner.Get(reference.GetHash()) != null;

        public bool ContainsKey(byte[] stateRoot) => stateRoot != null && _inner.Get(stateRoot) != null;

        public void Flush() { }

        public void Clear() { }
    }
}
