using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Nethereum.EVM.Precompiles.Backends
{
    /// <summary>
    /// Default P256VERIFY (EIP-7951) backend using BouncyCastle's
    /// <see cref="ECDsaSigner"/> over NIST P-256. Returns false on any
    /// failure (invalid point, bad signature, malformed input) — the
    /// handler is responsible for packaging 32-byte output from the
    /// boolean.
    /// </summary>
    public sealed class DefaultP256VerifyBackend : IP256VerifyBackend
    {
        public static readonly DefaultP256VerifyBackend Instance = new DefaultP256VerifyBackend();

        public bool Verify(byte[] hash, byte[] r, byte[] s, byte[] publicKeyX, byte[] publicKeyY)
        {
            var curveParams = NistNamedCurves.GetByName("P-256");
            var domainParams = new ECDomainParameters(
                curveParams.Curve, curveParams.G, curveParams.N, curveParams.H);

            var x = new Org.BouncyCastle.Math.BigInteger(1, publicKeyX);
            var y = new Org.BouncyCastle.Math.BigInteger(1, publicKeyY);
            var point = curveParams.Curve.CreatePoint(x, y);
            if (!point.IsValid()) return false;

            var rInt = new Org.BouncyCastle.Math.BigInteger(1, r);
            var sInt = new Org.BouncyCastle.Math.BigInteger(1, s);
            var pubKey = new ECPublicKeyParameters(point, domainParams);

            var signer = new ECDsaSigner();
            signer.Init(false, pubKey);

            return signer.VerifySignature(hash, rInt, sInt);
        }
    }
}
