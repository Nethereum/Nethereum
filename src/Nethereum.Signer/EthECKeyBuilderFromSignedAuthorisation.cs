using Nethereum.Model;

namespace Nethereum.Signer
{
    public static class EthECKeyBuilderFromSignedAuthorisation
    {
        public static EthECKey RecoverEthECKey(this Authorisation7702Signed authorisation7702Signed)
        {
            var signature = EthECDSASignatureFactory.FromSignature(authorisation7702Signed);
            var hash = authorisation7702Signed.EncodeAndHash();
            return EthECKey.RecoverFromParityYSignature(signature, hash);
        }
        
        public static string RecoverSignerAddress(this Authorisation7702Signed authorisation7702Signed)
        {
            return authorisation7702Signed.RecoverEthECKey().GetPublicAddress();
        }
    }
}