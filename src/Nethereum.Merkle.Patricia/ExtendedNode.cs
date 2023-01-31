
using Nethereum.Util.HashProviders;
using System.Collections.Generic;


namespace Nethereum.Merkle.Patricia
{
    public class ExtendedNode : Node
    {
        public byte[] Nibbles { get; set; }
        public Node InnerNode { get; set; }
        public ExtendedNode() : this(new Sha3KeccackHashProvider()) { }
        public ExtendedNode(IHashProvider hashProvider) : base(hashProvider)
        {
        }

        public override byte[] GetRLPEncodedData()
        {
            var returnByteArray = new List<byte[]>();
            var nibblesByteArray = GetPrefixedNibbles().ConvertFromNibbles();
            returnByteArray.Add(RLP.RLP.EncodeElement(nibblesByteArray));
            var encodedDataNextNode = InnerNode.GetRLPEncodedData();
            if(encodedDataNextNode.Length >= 32)
            {
                returnByteArray.Add(RLP.RLP.EncodeElement(InnerNode.GetHash()));
            }
            else
            {
                returnByteArray.Add(encodedDataNextNode);
            }
            return RLP.RLP.EncodeList(returnByteArray.ToArray());
        }

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
