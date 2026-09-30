using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    [CollectionDefinition("SignRecoverableBackendMutation", DisableParallelization = true)]
    public sealed class SignRecoverableBackendMutationCollection { }

    [Collection("SignRecoverableBackendMutation")]
    public class SignRecoverableBackendDifferentialTests
    {
        [Fact]
        public void Given_net8_plus_target_When_reading_default_Then_SignRecoverable_is_true()
        {
            Assert.True(EthECKey.SignRecoverable);
        }

        [Fact]
        public void Given_valid_sig_corpus_When_signed_both_backends_Then_r_s_v_byte_identical()
        {
            var original = EthECKey.SignRecoverable;
            try
            {
                foreach (var key in Keys())
                foreach (var hash in Hashes())
                {
                    EthECKey.SignRecoverable = false;
                    var bouncyCastle = key.SignAndCalculateV(hash);

                    EthECKey.SignRecoverable = true;
                    var nBitcoin = key.SignAndCalculateV(hash);

                    Assert.Equal(bouncyCastle.R.ToHex(), nBitcoin.R.ToHex());
                    Assert.Equal(bouncyCastle.S.ToHex(), nBitcoin.S.ToHex());
                    Assert.Equal(bouncyCastle.V.ToHex(), nBitcoin.V.ToHex());
                }
            }
            finally { EthECKey.SignRecoverable = original; }
        }

        [Fact]
        public void Given_valid_sig_corpus_When_recovered_both_backends_Then_address_byte_identical_and_matches_signer()
        {
            var original = EthECKey.SignRecoverable;
            try
            {
                foreach (var key in Keys())
                foreach (var hash in Hashes())
                {
                    var signature = key.SignAndCalculateV(hash);

                    EthECKey.SignRecoverable = false;
                    var bouncyCastle = EthECKey.RecoverFromSignature(signature, hash).GetPublicAddress();

                    EthECKey.SignRecoverable = true;
                    var nBitcoin = EthECKey.RecoverFromSignature(signature, hash).GetPublicAddress();

                    Assert.Equal(bouncyCastle.ToLowerInvariant(), nBitcoin.ToLowerInvariant());
                    Assert.Equal(key.GetPublicAddress().ToLowerInvariant(), nBitcoin.ToLowerInvariant());
                }
            }
            finally { EthECKey.SignRecoverable = original; }
        }

        [Fact]
        public void Given_a_tampered_hash_When_recovered_Then_address_differs_from_signer()
        {
            var original = EthECKey.SignRecoverable;
            try
            {
                var key = EthECKey.GenerateKey();
                var hash = Hash(0xAA);
                var signature = key.SignAndCalculateV(hash);

                var tampered = (byte[])hash.Clone();
                tampered[0] ^= 0x01;

                EthECKey.SignRecoverable = true;
                var recovered = EthECKey.RecoverFromSignature(signature, tampered)?.GetPublicAddress();

                Assert.NotEqual(key.GetPublicAddress().ToLowerInvariant(), recovered?.ToLowerInvariant());
            }
            finally { EthECKey.SignRecoverable = original; }
        }

        private static IEnumerable<EthECKey> Keys()
        {
            for (var i = 0; i < 8; i++)
                yield return EthECKey.GenerateKey();
        }

        private static IEnumerable<byte[]> Hashes()
        {
            yield return Hash(0x00);
            yield return Hash(0xFF);
            yield return Hash(0x01);
            yield return Hash(0x7F);
            yield return Hash(0x80);
            yield return Hash(0xAB);
        }

        private static byte[] Hash(byte fill)
        {
            var hash = new byte[32];
            for (var i = 0; i < hash.Length; i++)
                hash[i] = (byte)(fill ^ i);
            return hash;
        }
    }
}
