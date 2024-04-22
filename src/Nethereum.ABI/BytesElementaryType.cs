using Nethereum.ABI.Decoders;
using Nethereum.ABI.Encoders;

namespace Nethereum.ABI
{
    public class BytesElementaryType : ABIType
    {
        public override int StaticSize { get; }
        public BytesElementaryType(string name, int size) : base(name)
        {
            StaticSize = size;
            Decoder = new BytesElementaryTypeDecoder(size);
            Encoder = new BytesElementaryTypeEncoder(size);
        }
    }
}