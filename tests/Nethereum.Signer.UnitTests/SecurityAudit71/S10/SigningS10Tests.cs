using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Signer.UnitTests.SecurityAudit71.S10
{
    [Collection("SignRecoverableBackendMutation")]
    public class SigningS10Tests
    {
        private const string PrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe5129617082792ae468d01a3f362318";
        private static readonly BigInteger N = BigInteger.Parse("0fffffffffffffffffffffffffffffffebaaedce6af48a03bbfd25e8cd0364141", System.Globalization.NumberStyles.HexNumber);
        private const string Recipient = "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836";

        private static byte[] Hash(string text) => Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes(text));

        private static byte[] Word(BigInteger value)
        {
            var raw = value.ToByteArray().Reverse().SkipWhile(x => x == 0).ToArray();
            var word = new byte[32];
            Array.Copy(raw, 0, word, 32 - raw.Length, raw.Length);
            return word;
        }

        private static BigInteger ToInt(byte[] bytes) => new BigInteger(bytes.Reverse().Concat(new byte[] { 0 }).ToArray());

        private static void OnBothBackends(Action<string> action)
        {
            var original = EthECKey.SignRecoverable;
            var failures = new List<string>();
            try
            {
                foreach (var native in new[] { true, false })
                {
                    EthECKey.SignRecoverable = native;
                    var name = native ? "NBitcoin.Secp256k1" : "BouncyCastle";
                    try
                    {
                        action(name);
                    }
                    catch (Xunit.Sdk.XunitException ex)
                    {
                        failures.Add(ex.Message);
                    }
                }
            }
            finally
            {
                EthECKey.SignRecoverable = original;
            }
            Assert.True(failures.Count == 0, string.Join(" | ", failures));
        }

        private static string Join(byte[] r, byte[] s, byte v)
        {
            return "0x" + Word(ToInt(r)).ToHex() + Word(ToInt(s)).ToHex() + v.ToString("x2");
        }

        [Fact]
        public void S10_S01_Given_APrivateKeyOfZero_When_TheKeyIsConstructed_Then_ItIsRefused()
        {
            Assert.ThrowsAny<Exception>(() => new EthECKey(new byte[32], true));
        }

        [Fact]
        public void S10_S02_Given_APrivateKeyEqualToTheCurveOrder_When_TheKeyIsConstructed_Then_ItIsRefused()
        {
            Assert.ThrowsAny<Exception>(() => new EthECKey(Word(N), true));
        }

        [Fact]
        public void S10_S03_Given_APrivateKeyAboveTheCurveOrder_When_TheKeyIsConstructed_Then_ItIsRefused()
        {
            Assert.ThrowsAny<Exception>(() => new EthECKey(Word(N + 1), true));
        }

        [Fact]
        public void S10_S04_Given_TwoDistinctPrivateKeys_When_TheirAddressesAreDerived_Then_TheAddressesDiffer()
        {
            var one = new EthECKey(Word(1), true).GetPublicAddress();
            string aliased;
            try
            {
                aliased = new EthECKey(Word(N + 1), true).GetPublicAddress();
            }
            catch (Exception)
            {
                return;
            }
            Assert.NotEqual(one, aliased);
        }

        [Fact]
        public void S10_S05_Given_ThePointAtInfinityAsAPublicKey_When_TheKeyIsConstructed_Then_ItIsRefused()
        {
            Assert.ThrowsAny<Exception>(() => new EthECKey(new byte[] { 0 }, false));
        }

        [Fact]
        public void S10_S10_Given_AHighSMessageSignature_When_Recovered_Then_ItIsRefusedOrEqualsGethBehaviour()
        {
            var key = new EthECKey(PrivateKey);
            var hash = Hash("m");
            var signature = key.SignAndCalculateV(hash);
            Assert.True(signature.IsLowS);
            var flippedS = N - ToInt(signature.S);
            var flippedV = (byte)(signature.V[0] == 27 ? 28 : 27);
            var highS = EthECDSASignatureFactory.FromComponents(signature.R, Word(flippedS), flippedV);
            Assert.False(highS.IsLowS);
            var recovered = EthECKey.RecoverFromSignature(highS, hash);
            Assert.Equal(key.GetPublicAddress(), recovered.GetPublicAddress());
        }

        private static string SignedLegacyChainRlp(out LegacyTransactionChainId transaction, int chainId = 1)
        {
            transaction = new LegacyTransactionChainId(Recipient, 10, 3, 20, 21000, "", chainId);
            return new LegacyTransactionSigner().SignTransaction(new EthECKey(PrivateKey).GetPrivateKeyAsBytes(), transaction);
        }

        private static Signature HighS(ISignature signature)
        {
            var s = ToInt(signature.S);
            var v = signature.V.ToBigIntegerFromRLPDecoded();
            var flipped = v.IsEven ? v - 1 : v + 1;
            if (v == 0 || v == 1) flipped = 1 - v;
            return new Signature { R = signature.R, S = (N - s).ToBytesForRLPEncoding(), V = flipped.ToBytesForRLPEncoding() };
        }

        [Fact]
        public void S10_S12_Given_ATypedTransactionWithAHighS_When_Verified_Then_VerificationFails()
        {
            var transaction = new Transaction1559(1, 3, 2, 20, 21000, Recipient, 10, "", null);
            var rlp = new Transaction1559Signer().SignTransaction(PrivateKey, transaction);
            var decoded = TransactionFactory.CreateTransaction(rlp);
            Assert.True(decoded.VerifyTransaction());
            decoded.SetSignature(HighS(decoded.Signature));
            Assert.False(decoded.VerifyTransaction());
        }

        [Theory]
        [InlineData(27)]
        [InlineData(28)]
        [InlineData(2)]
        [InlineData(3)]
        public void S10_S14_Given_ATypedTransactionWithAYParityAboveOne_When_Verified_Then_VerificationFails(int v)
        {
            var transaction = new Transaction1559(1, 3, 2, 20, 21000, Recipient, 10, "", null);
            var rlp = new Transaction1559Signer().SignTransaction(PrivateKey, transaction);
            var decoded = TransactionFactory.CreateTransaction(rlp);
            var signature = decoded.Signature;
            decoded.SetSignature(new Signature { R = signature.R, S = signature.S, V = new[] { (byte)v } });
            Assert.False(decoded.VerifyTransaction());
        }

        [Fact]
        public void S10_S15_Given_AnEip155TransactionSignedForOneChain_When_TheChainIsChanged_Then_TheRecoveredSenderChanges()
        {
            var sender = new EthECKey(PrivateKey).GetPublicAddress();
            var transaction = new LegacyTransactionChainId(Recipient, 10, 3, 20, 21000, "", 1);
            var rlp = new LegacyTransactionSigner().SignTransaction(new EthECKey(PrivateKey).GetPrivateKeyAsBytes(), transaction);
            var decoded = (LegacyTransactionChainId)TransactionFactory.CreateTransaction(rlp);
            string other = null;
            try
            {
                other = EthECKey.RecoverFromSignature(EthECDSASignatureFactory.FromSignature(decoded.Signature), decoded.RawHash, 5)?.GetPublicAddress();
            }
            catch (Exception)
            {
            }
            Assert.False(other != null && other.IsTheSameAddress(sender));
        }

        [Theory]
        [InlineData("zero-s")]
        [InlineData("zero-r")]
        [InlineData("high-s")]
        [InlineData("r-n")]
        [InlineData("v-2")]
        [InlineData("v-27")]
        [InlineData("v-two-bytes")]
        public void S10_S16_Given_AMalformedAuthorisationSignature_When_TheAuthorityIsRecovered_Then_NoAuthorityIsReturned(string shape)
        {
            var authorisation = new Authorisation7702 { ChainId = 1, Address = Recipient, Nonce = 0 };
            var signed = new Authorisation7702Signer().SignAuthorisation(PrivateKey, authorisation);
            Assert.NotNull(signed.TryRecoverSignerAddress());
            var r = signed.R;
            var s = signed.S;
            var v = signed.V;
            switch (shape)
            {
                case "zero-s": s = new byte[] { 0 }; break;
                case "zero-r": r = new byte[] { 0 }; break;
                case "high-s": s = (N - ToInt(s)).ToBytesForRLPEncoding(); v = new[] { (byte)(1 - v[0]) }; break;
                case "r-n": r = N.ToBytesForRLPEncoding(); break;
                case "v-2": v = new byte[] { 2 }; break;
                case "v-27": v = new byte[] { 27 }; break;
                case "v-two-bytes": v = new byte[] { 0, 1 }; break;
            }
            var mutated = new Authorisation7702Signed(authorisation, r, s, v);
            Assert.Null(mutated.TryRecoverSignerAddress());
        }

        [Fact]
        public void S10_S17_Given_AnAuthorisationSignedForOneChain_When_TheChainIsChanged_Then_TheRecoveredAuthorityChanges()
        {
            var authorisation = new Authorisation7702 { ChainId = 1, Address = Recipient, Nonce = 0 };
            var signed = new Authorisation7702Signer().SignAuthorisation(PrivateKey, authorisation);
            var authority = signed.TryRecoverSignerAddress();
            var replayed = new Authorisation7702Signed(new Authorisation7702 { ChainId = 2, Address = Recipient, Nonce = 0 }, signed.R, signed.S, signed.V);
            Assert.False(authority.IsTheSameAddress(replayed.TryRecoverSignerAddress() ?? "none"));
        }

        [Fact]
        public void S10_S18_Given_AMultiByteUtf8Message_When_PersonalSigned_Then_TheEip191PrefixCountsBytes()
        {
            var message = "héllo €";
            var bytes = Encoding.UTF8.GetBytes(message);
            var expected = Sha3Keccack.Current.CalculateHash(new byte[] { 0x19 }.Concat(Encoding.UTF8.GetBytes("Ethereum Signed Message:\n" + bytes.Length)).Concat(bytes).ToArray());
            Assert.NotEqual(message.Length, bytes.Length);
            Assert.Equal(expected.ToHex(), new EthereumMessageSigner().HashPrefixedMessage(message).ToHex());
        }

        [Fact]
        public void S10_S19_Given_A32ByteHashAndAMessage_When_PersonalSigned_Then_TheTwoSignaturesDiffer()
        {
            var key = new EthECKey(PrivateKey);
            var signer = new EthereumMessageSigner();
            var hash = Hash("payload");
            var overHash = signer.Sign(hash, key);
            var overRawBytes = new MessageSigner().Sign(hash, key);
            Assert.NotEqual(overHash, overRawBytes);
        }

        [Fact]
        public void S10_S20_Given_TheSameHash_When_SignedTwiceOnEachBackend_Then_TheSignaturesAreIdenticalAndMatchRfc6979()
        {
            var key = new EthECKey("0x0000000000000000000000000000000000000000000000000000000000000001");
            var hash = System.Security.Cryptography.SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes("Satoshi Nakamoto"));
            var results = new List<string>();
            OnBothBackends(backend =>
            {
                var first = key.SignAndCalculateV(hash);
                var second = key.SignAndCalculateV(hash);
                Assert.Equal(first.R.ToHex(), second.R.ToHex());
                Assert.Equal(first.S.ToHex(), second.S.ToHex());
                results.Add(Word(ToInt(first.R)).ToHex() + Word(ToInt(first.S)).ToHex());
            });
            Assert.Equal("934b1ea10a4b3c1757e2b0c017d0b6143ce3c9a7e6a4a49860d7a6ab210ee3d82442ce9d2b916064108014783e923ec36b49743e2ffa1c4496f01a512aafd9e5", results[0]);
            Assert.Equal(results[0], results[1]);
        }

        [Fact]
        public void S10_S22_Given_A33ByteInputWhoseFirst32BytesMatch_When_SignedOnEachBackend_Then_TheBackendsAgree()
        {
            var key = new EthECKey(PrivateKey);
            var hash32 = Hash("agree");
            var hash33 = hash32.Concat(new byte[] { 0x99 }).ToArray();
            var outcomes = new List<string>();
            OnBothBackends(backend =>
            {
                try
                {
                    outcomes.Add(key.SignAndCalculateV(hash33).CreateStringSignature());
                }
                catch (Exception)
                {
                    outcomes.Add("refused");
                }
            });
            Assert.Equal(outcomes[0], outcomes[1]);
        }

        [Fact]
        public void S10_S25_Given_AKeyAndAHash_When_SignedOnEachBackend_Then_TheSignatureIsLowSAndVerifies()
        {
            var key = new EthECKey(PrivateKey);
            OnBothBackends(backend =>
            {
                for (var i = 0; i < 64; i++)
                {
                    var hash = Hash("sample" + i);
                    var signature = key.SignAndCalculateV(hash);
                    Assert.True(signature.IsLowS, backend);
                    Assert.True(key.VerifyAllowingOnlyLowS(hash, signature), backend);
                    Assert.True(EthECKey.RecoverFromSignature(signature, hash).GetPublicAddress().IsTheSameAddress(key.GetPublicAddress()), backend);
                }
            });
        }

    }
}
