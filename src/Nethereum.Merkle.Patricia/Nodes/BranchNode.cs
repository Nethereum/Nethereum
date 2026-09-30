using Nethereum.Util.HashProviders;

using Nethereum.Merkle.Patricia.Nodes.Rlp;
namespace Nethereum.Merkle.Patricia.Nodes
{
    public class BranchNode:Node
    {
        private readonly Node[] _children;
        private byte[] _value;

        public BranchNode() : this(Sha3KeccackHashProvider.Instance) { }
        public BranchNode(IHashProvider hashProvider) : base(hashProvider)
        {
            _children = new Node[16];
            _value = new byte[0];
        }

        public void SetChild(int nibble, Node node)
        {
            _children[nibble] = node;
            MarkDirty();
        }

        public void RemoveChild(int nibble)
        {
            _children[nibble] = null;
            MarkDirty();
        }

        public void CollapseChild(int nibble)
        {
            var child = _children[nibble];
            if (child == null || child is EmptyNode) return;
            if (child is HashNode hn)
            {
                if (hn.InnerNode == null) return;
                if (hn.InnerNode.Path == null) return;
                var h = hn.GetHash();
                hn.InnerNode = null;
                hn.Hash = h;
                return;
            }
            if (child.Path == null) return;
            _children[nibble] = new HashNode(HashProvider) { Hash = child.GetHash(), Owner = child.Owner, Path = child.Path };
        }

        public Node[] Children => _children;

        public byte[] Value
        {
            get => _value;
            set
            {
                _value = value;
                MarkDirty();
            }
        }

        protected override byte[] EncodeCore() => BranchNodeRlpSerializer.Encode(this);
    }
}
