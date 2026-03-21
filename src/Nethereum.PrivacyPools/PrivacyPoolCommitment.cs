using System;
using System.Numerics;
using System.Security.Cryptography;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.PrivacyPools
{
    public class PrivacyPoolCommitment
    {
        private static readonly PoseidonHasher HasherT1 = new PoseidonHasher(PoseidonParameterPreset.CircomT1);
        private static readonly PoseidonHasher HasherT2 = new PoseidonHasher(PoseidonParameterPreset.CircomT2);
        private static readonly PoseidonHasher HasherT3 = new PoseidonHasher(PoseidonParameterPreset.CircomT3);

        public BigInteger Value { get; }
        public BigInteger Label { get; }
        public BigInteger Nullifier { get; }
        public BigInteger Secret { get; }

        public BigInteger NullifierHash { get; }
        public BigInteger Precommitment { get; }
        public BigInteger CommitmentHash { get; }

        private PrivacyPoolCommitment(BigInteger value, BigInteger label, BigInteger nullifier, BigInteger secret)
        {
            Value = value;
            Label = label;
            Nullifier = nullifier;
            Secret = secret;

            NullifierHash = HasherT1.Hash(nullifier);
            Precommitment = HasherT2.Hash(nullifier, secret);
            CommitmentHash = HasherT3.Hash(value, label, Precommitment);
        }

        public static PrivacyPoolCommitment Create(BigInteger value, BigInteger label, BigInteger nullifier, BigInteger secret)
        {
            return new PrivacyPoolCommitment(value, label, nullifier, secret);
        }

        public static PrivacyPoolCommitment CreateRandom(BigInteger value, BigInteger label)
        {
            var nullifier = GenerateRandomFieldElement();
            var secret = GenerateRandomFieldElement();
            return new PrivacyPoolCommitment(value, label, nullifier, secret);
        }

        public static (BigInteger Nullifier, BigInteger Secret, BigInteger Precommitment) GenerateRandomPrecommitment()
        {
            var nullifier = GenerateRandomFieldElement();
            var secret = GenerateRandomFieldElement();
            var precommitment = HasherT2.Hash(nullifier, secret);
            return (nullifier, secret, precommitment);
        }

        public static BigInteger ComputeLabel(BigInteger scope, BigInteger nonce)
        {
            var scopeBytes = ToBigEndianBytes32(scope);
            var nonceBytes = ToBigEndianBytes32(nonce);

            var combined = new byte[64];
            Array.Copy(scopeBytes, 0, combined, 0, 32);
            Array.Copy(nonceBytes, 0, combined, 32, 32);

            var hash = Sha3Keccack.Current.CalculateHash(combined);
            var hashBigInt = new BigInteger(hash, isUnsigned: true, isBigEndian: true);
            return BigInteger.Remainder(hashBigInt, PrivacyPoolConstants.SNARK_SCALAR_FIELD);
        }

        private static byte[] ToBigEndianBytes32(BigInteger value)
        {
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (bytes.Length == 32) return bytes;
            var result = new byte[32];
            Array.Copy(bytes, 0, result, 32 - bytes.Length, bytes.Length);
            return result;
        }

        private static BigInteger GenerateRandomFieldElement()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
            return BigInteger.Remainder(value, PrivacyPoolConstants.SNARK_SCALAR_FIELD);
        }
    }
}
