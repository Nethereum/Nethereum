using System;
using System.Numerics;
using Nethereum.Accounts.Bip32;
using Nethereum.Util;

namespace Nethereum.PrivacyPools
{
    public class PrivacyPoolAccount
    {
        private static readonly PoseidonHasher HasherT1 = new PoseidonHasher(PoseidonParameterPreset.CircomT1);
        private static readonly PoseidonHasher HasherT3 = new PoseidonHasher(PoseidonParameterPreset.CircomT3);

        public BigInteger MasterNullifier { get; }
        public BigInteger MasterSecret { get; }

        public PrivacyPoolAccount(string mnemonic, string password = "")
        {
            var wallet = new MinimalHDWallet(mnemonic, password);

            var key1Bytes = wallet.GetKeyFromPath("m/44'/60'/0'/0/0").GetPrivateKeyAsBytes();
            var key2Bytes = wallet.GetKeyFromPath("m/44'/60'/1'/0/0").GetPrivateKeyAsBytes();

            var key1 = BytesToBigIntViaDouble(key1Bytes);
            var key2 = BytesToBigIntViaDouble(key2Bytes);

            MasterNullifier = HasherT1.Hash(key1);
            MasterSecret = HasherT1.Hash(key2);
        }

        public PrivacyPoolAccount(BigInteger masterNullifier, BigInteger masterSecret)
        {
            MasterNullifier = masterNullifier;
            MasterSecret = masterSecret;
        }

        public (BigInteger Nullifier, BigInteger Secret) CreateDepositSecrets(BigInteger scope, BigInteger depositIndex)
        {
            var nullifier = HasherT3.Hash(MasterNullifier, scope, depositIndex);
            var secret = HasherT3.Hash(MasterSecret, scope, depositIndex);
            return (nullifier, secret);
        }

        public (BigInteger Nullifier, BigInteger Secret) CreateWithdrawalSecrets(BigInteger label, BigInteger childIndex)
        {
            var nullifier = HasherT3.Hash(MasterNullifier, label, childIndex);
            var secret = HasherT3.Hash(MasterSecret, label, childIndex);
            return (nullifier, secret);
        }

        public BigInteger ComputePrecommitment(BigInteger nullifier, BigInteger secret)
        {
            return PrivacyPoolCommitment.Create(BigInteger.Zero, BigInteger.Zero, nullifier, secret).Precommitment;
        }

        public PrivacyPoolCommitment CreateDepositCommitment(BigInteger scope, BigInteger depositIndex, BigInteger value, BigInteger label)
        {
            var (nullifier, secret) = CreateDepositSecrets(scope, depositIndex);
            return PrivacyPoolCommitment.Create(value, label, nullifier, secret);
        }

        public PrivacyPoolCommitment CreateWithdrawalCommitment(BigInteger label, BigInteger childIndex, BigInteger value)
        {
            var (nullifier, secret) = CreateWithdrawalSecrets(label, childIndex);
            return PrivacyPoolCommitment.Create(value, label, nullifier, secret);
        }

        // Replicates viem's bytesToNumber() behavior which converts 32-byte private keys
        // through JavaScript's Number type (IEEE 754 double, ~53 bits mantissa), losing
        // precision for values > 2^53. The 0xbow SDK uses bytesToNumber() in generateMasterKeys(),
        // so we must match this lossy conversion for cross-compatibility.
        // See: https://github.com/0xbow-io/privacy-pools-core/packages/sdk/src/crypto.ts
        public static BigInteger BytesToBigIntViaDouble(byte[] bytes)
        {
            double value = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                value = value * 256 + bytes[i];
            }
            return (BigInteger)value;
        }
    }
}
