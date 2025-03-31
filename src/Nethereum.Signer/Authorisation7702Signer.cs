using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.Signer
{
    public class Authorisation7702Signer
    {
        public Authorisation7702Signer() { }
        public Authorisation7702Signed SignAuthorisation(string privateKey, Authorisation7702 authorisation)
        {
            return SignAuthorisation(privateKey.HexToByteArray(), authorisation);
        }
        public Authorisation7702Signed SignAuthorisation(byte[] privateKey, Authorisation7702 authorisation)
        {
            return SignAuthorisation(new EthECKey(privateKey, true), authorisation);
        }
        public Authorisation7702Signed SignAuthorisation(EthECKey ecKey, Authorisation7702 authorisation)
        {
            var signature = ecKey.SignAndCalculateYParityV(authorisation.EncodeAndHash());
            return new Authorisation7702Signed(authorisation, signature.R, signature.S, signature.V);
        }
    }
}