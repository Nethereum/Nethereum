using Nethereum.ABI.Decoders;
using Nethereum.ABI.Encoders;

namespace Nethereum.ABI
{
    public class BytesType : ABIType
    {
        public BytesType() : base("bytes")
        {
            this.Decoder = new BytesTypeDecoder();
            this.Encoder = new BytesTypeEncoder();
        }

        public override int FixedSize => -1;
        
    }
}