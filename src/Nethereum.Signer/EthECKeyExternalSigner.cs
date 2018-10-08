using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;

namespace Nethereum.Signer
{
#if !DOTNET35
    public class EthECKeyExternalSigner: IEthECKeyExternalSigner
    {
        private readonly IEthExternalSigner _ethExternalSigner;

        public EthECKeyExternalSigner(IEthExternalSigner ethExternalSigner)
        {
            _ethExternalSigner = ethExternalSigner;
        }

        public async Task<string> GetPublicKeyAsync()
        {
            var publicKey = await _ethExternalSigner.GetPublicKeyAsync();
            return publicKey.ToHex(true);
        }

        public ExternalSignerFormat ExternalSignerFormat => _ethExternalSigner.ExternalSignerFormat;

        public async Task<string> GetAddressAsync()
        {
             var publicKey = await _ethExternalSigner.GetPublicKeyAsync();
             return new EthECKey(publicKey, false).GetPublicAddress();
        }

        public async Task<EthECDSASignature> SignAndCalculateVAsync(byte[] hash, BigInteger chainId)
        { 
            var signature = await _ethExternalSigner.SignAsync(hash);
            if (_ethExternalSigner.CalculatesV) return new EthECDSASignature(signature);

            var publicKey = await _ethExternalSigner.GetPublicKeyAsync();
            var recId = EthECKey.CalculateRecId(signature, hash, publicKey);
            var vChain = EthECKey.CalculateV(chainId, recId);
            signature.V = vChain.ToBytesForRLPEncoding();
            return new EthECDSASignature(signature);
        }

        public async Task<EthECDSASignature> SignAndCalculateVAsync(byte[] hash)
        {
            var signature = await _ethExternalSigner.SignAsync(hash);
            if (_ethExternalSigner.CalculatesV) return new EthECDSASignature(signature);

            var publicKey = await _ethExternalSigner.GetPublicKeyAsync();
            var recId = EthECKey.CalculateRecId(signature, hash, publicKey);
            signature.V = new[] { (byte)(recId + 27) };
            return new EthECDSASignature(signature);
        }
    }
#endif
}