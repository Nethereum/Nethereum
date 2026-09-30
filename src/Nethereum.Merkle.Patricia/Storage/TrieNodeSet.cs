using Nethereum.Documentation;
using System.Collections.Generic;

using Nethereum.Merkle.Patricia.Nodes;
namespace Nethereum.Merkle.Patricia.Storage
{
    public class TrieNodeSet
    {
        private readonly List<Node> _nodes = new List<Node>();
        private readonly List<TrieNodeDelete> _deletes = new List<TrieNodeDelete>();

        public IReadOnlyList<Node> Nodes => _nodes;

        public IReadOnlyList<TrieNodeDelete> Deletes => _deletes;

        public int Count => _nodes.Count;

        public void Add(Node node)
        {
            if (node != null) _nodes.Add(node);
        }

        public void AddDelete(byte[] owner, byte[] path, byte[] prevBlob)
        {
            _deletes.Add(new TrieNodeDelete(owner, path, prevBlob));
        }
    }

    [NethereumDocExample(DocSection.ChainInfrastructure, "key-path-storage", "The tombstone a path-store commit emits for a removed node")]
    public readonly struct TrieNodeDelete
    {
        public byte[] Owner { get; }
        public byte[] Path { get; }
        public byte[] PrevBlob { get; }

        public TrieNodeDelete(byte[] owner, byte[] path, byte[] prevBlob)
        {
            Owner = owner;
            Path = path;
            PrevBlob = prevBlob;
        }
    }
}
