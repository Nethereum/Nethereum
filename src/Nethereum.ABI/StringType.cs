using Nethereum.ABI.Decoders;
using Nethereum.ABI.Encoders;

namespace Nethereum.ABI
{
    public class StringType : ABIType
    {

        public StringType() : base("string")
        {
            this.Decoder = new StringTypeDecoder();
            this.Encoder = new StringTypeEncoder();
        }

         public override int FixedSize => -1;
    }
}