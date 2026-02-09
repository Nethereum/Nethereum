using Nethereum.Util.HashProviders;
using System.Collections.Generic;



namespace Nethereum.Merkle.Patricia
{
    public class LeafNode:Node
    {
        private byte[] _nibbles;
        private byte[] _value;

        public LeafNode() : this(new Sha3KeccackHashProvider())
        {

        }
        public LeafNode(IHashProvider hashProvider) : base(hashProvider)
        {

        }

        public byte[] Nibbles
        {
            get => _nibbles;
            set
            {
                _nibbles = value;
                MarkDirty();
            }
        }

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
