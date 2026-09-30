using System.Numerics;
using Nethereum.CoreChain;
using Nethereum.Merkle.Binary.Keys;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class AccountLayoutStrategyTests
    {
        [Fact]
        public void Rlp_RoundTrip_PreservesAllFields()
        {
            var original = new Account
            {
                Nonce = 42,
                Balance = 1_000_000_000_000_000_000UL,
                StateRoot = FilledBytes(0xAA, 32),
                CodeHash = FilledBytes(0xBB, 32)
            };

            var encoded = RlpAccountLayout.Instance.EncodeAccount(original);
            var decoded = RlpAccountLayout.Instance.DecodeAccount(encoded);

            Assert.NotNull(decoded);
            Assert.Equal((BigInteger)original.Nonce, (BigInteger)decoded.Nonce);
            Assert.Equal((BigInteger)original.Balance, (BigInteger)decoded.Balance);
            Assert.Equal(original.StateRoot, decoded.StateRoot);
            Assert.Equal(original.CodeHash, decoded.CodeHash);
            Assert.False(RlpAccountLayout.Instance.HasExternalCodeHash);
        }

        [Fact]
        public void Rlp_DecodesEmptyAsNull()
        {
            Assert.Null(RlpAccountLayout.Instance.DecodeAccount(null));
            Assert.Null(RlpAccountLayout.Instance.DecodeAccount(new byte[0]));
        }

        [Fact]
        public void BinaryPacked_ProducesExactly32Bytes()
        {
            var account = new Account
            {
                Nonce = 1,
                Balance = 100
            };

            var encoded = BinaryPackedAccountLayout.Instance.EncodeAccount(account);
            Assert.Equal(32, encoded.Length);
        }

        [Fact]
        public void BinaryPacked_LayoutMatchesEip7864FieldOffsets()
        {
            var account = new Account
            {
                Nonce = 0x01_02_03_04_05_06_07_08UL,
                Balance = new EvmUInt256(0xA1A2A3A4UL)
            };

            var encoded = BinaryPackedAccountLayout.Instance.EncodeAccount(account);

            Assert.Equal(0, encoded[BasicDataLeaf.VersionOffset]);

            Assert.Equal(0, encoded[BasicDataLeaf.CodeSizeOffset]);
            Assert.Equal(0, encoded[BasicDataLeaf.CodeSizeOffset + 1]);
            Assert.Equal(0, encoded[BasicDataLeaf.CodeSizeOffset + 2]);

            Assert.Equal(0x01, encoded[BasicDataLeaf.NonceOffset]);
            Assert.Equal(0x02, encoded[BasicDataLeaf.NonceOffset + 1]);
            Assert.Equal(0x08, encoded[BasicDataLeaf.NonceOffset + 7]);

            Assert.Equal(0xA1, encoded[BasicDataLeaf.BalanceOffset + 12]);
            Assert.Equal(0xA2, encoded[BasicDataLeaf.BalanceOffset + 13]);
            Assert.Equal(0xA3, encoded[BasicDataLeaf.BalanceOffset + 14]);
            Assert.Equal(0xA4, encoded[BasicDataLeaf.BalanceOffset + 15]);
        }

        [Fact]
        public void BinaryPacked_RoundTrip_PreservesNonceAndBalance()
        {
            var original = new Account
            {
                Nonce = 123,
                Balance = new EvmUInt256(0xDEAD_BEEF_CAFE_BABEUL)
            };

            var encoded = BinaryPackedAccountLayout.Instance.EncodeAccount(original);
            var decoded = BinaryPackedAccountLayout.Instance.DecodeAccount(encoded);

            Assert.NotNull(decoded);
            Assert.Equal((BigInteger)original.Nonce, (BigInteger)decoded.Nonce);
            Assert.Equal((BigInteger)original.Balance, (BigInteger)decoded.Balance);
        }

        [Fact]
        public void BinaryPacked_DecodeDropsCodeHashAndStateRoot()
        {
            var original = new Account
            {
                Nonce = 5,
                Balance = 42,
                CodeHash = FilledBytes(0xCC, 32),
                StateRoot = FilledBytes(0xDD, 32)
            };

            var encoded = BinaryPackedAccountLayout.Instance.EncodeAccount(original);
            var decoded = BinaryPackedAccountLayout.Instance.DecodeAccount(encoded);

            Assert.Null(decoded.CodeHash);
            Assert.Null(decoded.StateRoot);
            Assert.True(BinaryPackedAccountLayout.Instance.HasExternalCodeHash);
        }

        [Fact]
        public void BinaryPacked_DecodesEmptyAsNull()
        {
            Assert.Null(BinaryPackedAccountLayout.Instance.DecodeAccount(null));
            Assert.Null(BinaryPackedAccountLayout.Instance.DecodeAccount(new byte[0]));
        }

        [Fact]
        public void BinaryPacked_And_Rlp_ProduceDifferentBytes()
        {
            var account = new Account { Nonce = 1, Balance = 100 };
            var rlp = RlpAccountLayout.Instance.EncodeAccount(account);
            var binary = BinaryPackedAccountLayout.Instance.EncodeAccount(account);
            Assert.NotEqual(rlp, binary);
            Assert.Equal(32, binary.Length);
        }

        [Fact]
        public void BinaryPacked_CodeHash_PreservedThroughExternalSlot()
        {
            var layout = BinaryPackedAccountLayout.Instance;
            Assert.True(layout.HasExternalCodeHash);

            var original = new Account
            {
                Nonce = 5,
                Balance = new EvmUInt256(1000),
                CodeHash = Nethereum.Util.Sha3Keccack.Current.CalculateHash(
                    new byte[] { 0x60, 0x01, 0x60, 0x00, 0x55 })
            };

            var encoded = layout.EncodeAccount(original);
            Assert.Equal(32, encoded.Length);

            var decoded = layout.DecodeAccount(encoded);
            Assert.Null(decoded.CodeHash);

            decoded.CodeHash = original.CodeHash;
            Assert.Equal(original.CodeHash, decoded.CodeHash);

            Assert.Equal((System.Numerics.BigInteger)original.Nonce, (System.Numerics.BigInteger)decoded.Nonce);
            Assert.Equal((System.Numerics.BigInteger)original.Balance, (System.Numerics.BigInteger)decoded.Balance);
        }

        private static byte[] FilledBytes(byte fill, int length)
        {
            var b = new byte[length];
            for (var i = 0; i < length; i++) b[i] = fill;
            return b;
        }
    }
}
