using Nethereum.ABI.Decoders;
using Nethereum.ABI.Encoders;

namespace Nethereum.ABI
{
    public class IntType : ABIType
    {
        public IntType(string name) : base(name)
        {
            Decoder = new IntTypeDecoder(IsSigned(name));
            Encoder = new IntTypeEncoder(IsSigned(name));
        }

        private static bool IsSigned(string name)
        {
            return !name.ToLower().StartsWith("u");
        }

        public override string CanonicalName
        {
            get
            {
                if (Name.Equals("int"))
                    return "int256";
                if (Name.Equals("uint"))
                    return "uint256";
                return base.CanonicalName;
            }
        }
    }
}