using Nethereum.ABI.Decoders;
using Nethereum.ABI.Encoders;

namespace Nethereum.ABI
{
    public abstract class FixedSizeBytesType : ABIType
    {
        public FixedSizeBytesType(string name, byte arraySize) : base(name)
        {
            Decoder = new FixedSizeBytesTypeDecoder(arraySize);
            Encoder = new FixedSizeBytesTypeEncoder(arraySize);
        }
    }
}