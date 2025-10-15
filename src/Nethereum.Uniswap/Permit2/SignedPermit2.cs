using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Uniswap.Permit2
{
    public class SignedPermit2<TPermitRequest>
    {
        public TPermitRequest PermitRequest { get; set; }
        public string Signature { get; set; }

        public byte[] GetSignatureBytes()
        {
           return Signature.HexToByteArray();
        }
    }
}
