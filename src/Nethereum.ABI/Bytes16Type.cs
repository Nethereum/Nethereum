using Nethereum.ABI.Decoders;
using Nethereum.ABI.Encoders;

namespace Nethereum.ABI
{
    public class Bytes16Type : FixedSizeBytesType
    {
        public Bytes16Type(string name) : base(name, 16)
        {
    
        }
    }
}