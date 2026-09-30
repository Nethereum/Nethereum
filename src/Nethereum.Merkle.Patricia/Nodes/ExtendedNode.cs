
using Nethereum.Util.HashProviders;
using System.Collections.Generic;


using Nethereum.Merkle.Patricia.Nodes.Rlp;
namespace Nethereum.Merkle.Patricia.Nodes
{
    public class ExtendedNode : Node
    {
        private byte[] _nibbles;
        private Node _innerNode;

        public byte[] Nibbles
        {
            get => _nibbles;
            set
            {
                _nibbles = value;
                MarkDirty();
            }
        }

        public Node InnerNode
        {
            get => _innerNode;
            set
            {
                _innerNode = value;
                MarkDirty();
            }
        }

        public ExtendedNode() : this(Sha3KeccackHashProvider.Instance) { }
        public ExtendedNode(IHashProvider hashProvider) : base(hashProvider)
        {
        }

        public void CollapseInner()
        {
            if (_innerNode == null || _innerNode is EmptyNode) return;
            if (_innerNode is HashNode hn)
            {
                if (hn.InnerNode == null) return;
                if (hn.InnerNode.Path == null) return;
                var h = hn.GetHash();
                hn.InnerNode = null;
                hn.Hash = h;
                return;
            }
            if (_innerNode.Path == null) return;
            _innerNode = new HashNode(HashProvider) { Hash = _innerNode.GetHash(), Owner = _innerNode.Owner, Path = _innerNode.Path };
        }

        protected override byte[] EncodeCore() => ExtendedNodeRlpSerializer.Encode(this);

        public byte[] GetPrefixedNibbles()
        {
            var returnArray = new List<byte>();
            if (Nibbles.Length % 2 > 0)
            {
                returnArray.Add(1);

            }
            else
            {
                returnArray.Add(0);
                returnArray.Add(0);
            }

            returnArray.AddRange(Nibbles);
            return returnArray.ToArray();
        }
    }
}
