using Nethereum.Util.HashProviders;
using System.Collections.Generic;

namespace Nethereum.Merkle.Patricia
{
    public class BranchNode:Node
    {
        public BranchNode() : this(new Sha3KeccackHashProvider()) { }
        public BranchNode(IHashProvider hashProvider) : base(hashProvider)
        {
            Children = new Node[16];
            Value = new byte[0];
        }

        public void SetChild(int nibble, Node node)
        {
            Children[nibble] = node;
        }

        public void RemoveChild(int nibble)
        {
            Children[nibble] = null;
        }

        public Node[] Children { get; }
        public byte[] Value { get; set; }

        public override byte[] GetRLPEncodedData()
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
