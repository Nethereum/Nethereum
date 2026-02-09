using Nethereum.Util.HashProviders;
using System.Collections.Generic;

namespace Nethereum.Merkle.Patricia
{
    public class BranchNode:Node
    {
        private readonly Node[] _children;
        private byte[] _value;

        public BranchNode() : this(new Sha3KeccackHashProvider()) { }
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

        public override byte[] GetRLPEncodedDataCore()
        {
            var returnByteArray = new List<byte[]>();
            foreach(Node node in Children)
            {
                if(node is null)
                {
                    returnByteArray.Add(new EmptyNode().GetRLPEncodedData());
                }
                else
                {
                    var encodedDataNextNode = node.GetRLPEncodedData();
                    if (encodedDataNextNode.Length >= 32)
                    {
                        returnByteArray.Add(RLP.RLP.EncodeElement(node.GetHash()));
                    }
                    else
                    {
                        returnByteArray.Add(encodedDataNextNode);
                    }
                    
                }
            }
            returnByteArray.Add(RLP.RLP.EncodeElement(Value));
            return RLP.RLP.EncodeList(returnByteArray.ToArray());
        }
    }
}
