using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Enr;
using Nethereum.Signer.Crypto;
using Nethereum.Signer.Enr;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    [Collection("SignRecoverableBackendMutation")]
    public class FoundationsSignerDocExampleTests
    {
        private static readonly byte[] G1Doubled = ("0000000000000000000000000000000000000000000000000000000000000001" +
            "0000000000000000000000000000000000000000000000000000000000000002" +
            "0000000000000000000000000000000000000000000000000000000000000001" +
            "0000000000000000000000000000000000000000000000000000000000000002").HexToByteArray();

        private static EthECDSASignature UnrecoverableSignature(byte[] hash)
        {
            for (byte r = 1; r <= 64; r++)
            {
                var rBytes = new byte[32];
                rBytes[31] = r;
                var sBytes = new byte[32];
                sBytes[31] = 1;
                var signature = EthECDSASignatureFactory.FromComponents(rBytes, sBytes, new byte[] { 27 });
                if (EthECKey.RecoverFromSignature(signature, hash) == null) return signature;
            }
            throw new InvalidOperationException("no unrecoverable signature found");
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "signer-secp256k1-contexts", "Build the secp256k1 contexts at startup")]
        public void Given_TheSecp256k1Contexts_When_EnsureInitialisedIsCalled_Then_TheyAreReportedInitialised()
        {
            Secp256k1Contexts.EnsureInitialised();
            Secp256k1Contexts.EnsureInitialised();

            Assert.True(Secp256k1Contexts.IsInitialised);
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "signer-recover-null", "RecoverFromSignature returns null when no key recovers; EcRecover throws")]
        public void Given_ASignatureThatRecoversNoKey_When_Recovered_Then_RecoverFromSignatureReturnsNullAndEcRecoverThrows()
        {
            var hash = new Nethereum.Util.Sha3Keccack().CalculateHash(new byte[] { 1, 2, 3 });
            var signature = UnrecoverableSignature(hash);

            Assert.Null(EthECKey.RecoverFromSignature(signature, hash));
            Assert.Throws<ArgumentException>(() => new MessageSigner().EcRecover(hash, signature));
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "signer-recover-parity", "VerifyTransaction rejects a typed transaction with a y-parity above one")]
        public void Given_AYParityAboveOne_When_TheTypedTransactionIsVerified_Then_VerificationFails()
        {
            var transaction = new Nethereum.Model.Transaction1559(1, 3, 2, 20, 21000, "0x1111111111111111111111111111111111111111", 10, "", null);
            var rlp = new Transaction1559Signer().SignTransaction("0x4c0883a69102937d6231471b5dbb6204fe5129617082792ae468d01a3f362318", transaction);
            var decoded = Nethereum.Model.TransactionFactory.CreateTransaction(rlp);
            Assert.True(decoded.VerifyTransaction());
            var signature = decoded.Signature;
            decoded.SetSignature(new Nethereum.Model.Signature { R = signature.R, S = signature.S, V = new byte[] { 2 } });
            Assert.False(decoded.VerifyTransaction());
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "signer-bn128-try", "BN128Curve.TryAdd reports invalid input as false")]
        public void Given_AnInvalidBn128Point_When_AddedWithTryAddAndAdd_Then_TryAddReturnsFalseAndAddThrows()
        {
            var valid = BN128Curve.TryAdd(G1Doubled, out var sum);

            var offCurve = new byte[128];
            offCurve[31] = 1;
            offCurve[63] = 1;
            var invalid = BN128Curve.TryAdd(offCurve, out var none);

            Assert.True(valid);
            Assert.Equal(64, sum.Length);
            Assert.False(invalid);
            Assert.Null(none);
            Assert.Throws<ArgumentException>(() => BN128Curve.Add(offCurve));
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "signer-enr-low-s", "EnrRecordSigner.Verify rejects a high-S signature")]
        public void Given_ASignedEnrRecordWhoseSignatureIsMadeHighS_When_Verified_Then_ItIsRejected()
        {
            var key = EthECKey.GenerateKey();
            var record = new EnrRecord { Sequence = 1 };
            record.Pairs["ip"] = new byte[] { 8, 8, 4, 4 };
            EnrRecordSigner.Sign(record, key);
            Assert.True(EnrRecordSigner.Verify(record));

            var order = new Org.BouncyCastle.Math.BigInteger("fffffffffffffffffffffffffffffffebaaedce6af48a03bbfd25e8cd0364141", 16);
            var s = new Org.BouncyCastle.Math.BigInteger(1, record.Signature.Skip(32).Take(32).ToArray());
            var highS = order.Subtract(s).ToByteArrayUnsigned();
            var padded = new byte[32];
            Array.Copy(highS, 0, padded, 32 - highS.Length, highS.Length);
            record.Signature = record.Signature.Take(32).Concat(padded).ToArray();

            Assert.False(EnrRecordSigner.Verify(record));
        }
    }
}
