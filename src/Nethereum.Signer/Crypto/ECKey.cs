using System;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;

namespace Nethereum.Signer.Crypto
{
    /// <summary>
    ///     ECKey based on the implementation of NBitcoin
    ///     https://github.com/MetacoSA/NBitcoin
    ///     Removed the dependency of the custom BouncyCastle
    /// </summary>
    public class ECKey
    {
        public static readonly BigInteger HALF_CURVE_ORDER;
        public static readonly BigInteger CURVE_ORDER;
        public static readonly ECDomainParameters CURVE;
        public static readonly X9ECParameters _Secp256k1;
        private readonly ECKeyParameters _Key;

        private ECDomainParameters _DomainParameter;

        static ECKey()
        {
            //using Bouncy
            _Secp256k1 = SecNamedCurves.GetByName("secp256k1");
            CURVE = new ECDomainParameters(_Secp256k1.Curve, _Secp256k1.G, _Secp256k1.N, _Secp256k1.H);
            HALF_CURVE_ORDER = _Secp256k1.N.ShiftRight(1);
            CURVE_ORDER = _Secp256k1.N;
        }

        public ECKey(byte[] vch, bool isPrivate)
        {
            if (isPrivate)
            {
                _Key = new ECPrivateKeyParameters(new BigInteger(1, vch), DomainParameter);
            }
            else
            {
                var q = Secp256k1.Curve.DecodePoint(vch);
                _Key = new ECPublicKeyParameters("EC", q, DomainParameter);
            }
        }

        public ECPrivateKeyParameters PrivateKey => _Key as ECPrivateKeyParameters;


        public static X9ECParameters Secp256k1 => _Secp256k1;

        public ECDomainParameters DomainParameter
        {
            get
            {
                if (_DomainParameter == null)
                    _DomainParameter = new ECDomainParameters(Secp256k1.Curve, Secp256k1.G, Secp256k1.N, Secp256k1.H);
                return _DomainParameter;
            }
        }


        public byte[] GetPubKey(bool isCompressed)
        {
            var q = GetPublicKeyParameters().Q;
            //Pub key (q) is composed into X and Y, the compressed form only include X, which can derive Y along with 02 or 03 prepent depending on whether Y in even or odd.
            q = q.Normalize();
            var result =
                Secp256k1.Curve.CreatePoint(q.XCoord.ToBigInteger(), q.YCoord.ToBigInteger()).GetEncoded(isCompressed);
            return result;
        }

        public ECPublicKeyParameters GetPublicKeyParameters()
        {
            if (_Key is ECPublicKeyParameters)
                return (ECPublicKeyParameters) _Key;
            var q = Secp256k1.G.Multiply(PrivateKey.D);
            return new ECPublicKeyParameters("EC", q, DomainParameter);
        }


        public static ECKey RecoverFromSignature(int recId, ECDSASignature sig, byte[] message, bool compressed)
        {
            if (recId < 0)
                throw new ArgumentException("recId should be positive");
            if (sig.R.SignValue < 0)
                throw new ArgumentException("r should be positive");
            if (sig.S.SignValue < 0)
                throw new ArgumentException("s should be positive");
            if (message == null)
                throw new ArgumentNullException("message");


            var curve = Secp256k1;

            // 1.0 For j from 0 to h   (h == recId here and the loop is outside this function)
            //   1.1 Let x = r + jn

            var n = curve.N;
            var i = BigInteger.ValueOf((long) recId / 2);
            var x = sig.R.Add(i.Multiply(n));

            //   1.2. Convert the integer x to an octet string X of length mlen using the conversion routine
            //        specified in Section 2.3.7, where mlen = ⌈(log2 p)/8⌉ or mlen = ⌈m/8⌉.
            //   1.3. Convert the octet string (16 set binary digits)||X to an elliptic curve point R using the
            //        conversion routine specified in Section 2.3.4. If this conversion routine outputs “invalid”, then
            //        do another iteration of Step 1.
            //
            // More concisely, what these points mean is to use X as a compressed public key.

            //using bouncy and Q value of Point
            var prime = new BigInteger(1,
                Org.BouncyCastle.Utilities.Encoders.Hex.Decode(
                    "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFC2F"));
            if (x.CompareTo(prime) >= 0)
                return null;

            // Compressed keys require you to know an extra bit of data about the y-coord as there are two possibilities.
            // So it's encoded in the recId.
            var R = DecompressKey(x, (recId & 1) == 1);
            //   1.4. If nR != point at infinity, then do another iteration of Step 1 (callers responsibility).

            if (!R.Multiply(n).IsInfinity)
                return null;

            //   1.5. Compute e from M using Steps 2 and 3 of ECDSA signature verification.
            var e = new BigInteger(1, message);
            //   1.6. For k from 1 to 2 do the following.   (loop is outside this function via iterating recId)
            //   1.6.1. Compute a candidate public key as:
            //               Q = mi(r) * (sR - eG)
            //
            // Where mi(x) is the modular multiplicative inverse. We transform this into the following:
            //               Q = (mi(r) * s ** R) + (mi(r) * -e ** G)
            // Where -e is the modular additive inverse of e, that is z such that z + e = 0 (mod n). In the above equation
            // ** is point multiplication and + is point addition (the EC group operator).
            //
            // We can find the additive inverse by subtracting e from zero then taking the mod. For example the additive
            // inverse of 3 modulo 11 is 8 because 3 + 8 mod 11 = 0, and -3 mod 11 = 8.

            var eInv = BigInteger.Zero.Subtract(e).Mod(n);
            var rInv = sig.R.ModInverse(n);
            var srInv = rInv.Multiply(sig.S).Mod(n);
            var eInvrInv = rInv.Multiply(eInv).Mod(n);
            var q = ECAlgorithms.SumOfTwoMultiplies(curve.G, eInvrInv, R, srInv);
            q = q.Normalize();
            if (compressed)
            {
                q = Secp256k1.Curve.CreatePoint(q.XCoord.ToBigInteger(), q.YCoord.ToBigInteger());
                return new ECKey(q.GetEncoded(true), false);
            }
            return new ECKey(q.GetEncoded(), false);
        }


        public virtual ECDSASignature Sign(byte[] hash)
        {
            AssertPrivateKey();
            var signer = new DeterministicECDSA();
            signer.setPrivateKey(PrivateKey);
            var sig = ECDSASignature.FromDER(signer.signHash(hash));
            return sig.MakeCanonical();
        }

        public bool Verify(byte[] hash, ECDSASignature sig)
        {
            var signer = new ECDsaSigner();
            signer.Init(false, GetPublicKeyParameters());
            return signer.VerifySignature(hash, sig.R, sig.S);
        }

        private void AssertPrivateKey()
        {
            if (PrivateKey == null)
                throw new InvalidOperationException("This key should be a private key for such operation");
        }

        private static ECPoint DecompressKey(BigInteger xBN, bool yBit)
        {
            var curve = Secp256k1.Curve;
            var compEnc = X9IntegerConverter.IntegerToBytes(xBN, 1 + X9IntegerConverter.GetByteLength(curve));
            compEnc[0] = (byte) (yBit ? 0x03 : 0x02);
            return curve.DecodePoint(compEnc);
        }
    }
}