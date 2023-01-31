using Nethereum.Util.HashProviders;
using System.Collections.Generic;



namespace Nethereum.Merkle.Patricia
{
    public class LeafNode:Node
    {
        public LeafNode() : this(new Sha3KeccackHashProvider())
        {

        }
        public LeafNode(IHashProvider hashProvider) : base(hashProvider)
        {

        }

        public byte[] Nibbles { get; set; }
        public byte[] Value { get; set; }

        public override byte[] GetRLPEncodedData()
        {
            var returnByteArray = new List<byte[]>();
            var nibblesByteArray = GetPrefixedNibbles().ConvertFromNibbles();
            returnByteArray.Add(RLP.RLP.EncodeElement(nibblesByteArray));
            returnByteArray.Add(RLP.RLP.EncodeElement(Value));
            return RLP.RLP.EncodeList(returnByteArray.ToArray());
        }

        public byte[] GetPrefixedNibbles()
        {
            var returnArray = new List<byte>();
            if (Nibbles.Length % 2 > 0)
            {
                returnArray.Add(3);
               
            }
            else
            {
                returnArray.Add(2);
                returnArray.Add(0);
            }

            returnArray.AddRange(Nibbles);
            return returnArray.ToArray();
        }
    }
}
