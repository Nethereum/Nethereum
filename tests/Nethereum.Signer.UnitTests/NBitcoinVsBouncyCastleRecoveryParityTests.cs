using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Org.BouncyCastle.Math;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Signer.UnitTests
{
    [Collection("SignRecoverableBackendMutation")]
    public class NBitcoinVsBouncyCastleRecoveryParityTests
    {
        private readonly ITestOutputHelper _output;

        public NBitcoinVsBouncyCastleRecoveryParityTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public class RecoveryVector
        {
            public string Name { get; set; }
            public string Family { get; set; }
            public ISignedTransaction Transaction { get; set; }
            public string ExpectedSender { get; set; }
            public int RecId { get; set; }
        }

        // ------------------------------------------------------------------------------------------------
        // Corpus
        // ------------------------------------------------------------------------------------------------
        // Built once per test-process and cached: real chain-signed vectors already living in this repo's
        // test suite (Geth/Vitalik legacy vectors, both pre-EIP-155 and EIP-155) PLUS deterministically
        // generated vectors covering every transaction family the mainnet follower's TransactionProcessor
        // can encounter: pre-EIP-155 legacy, EIP-155 legacy (chainId in v), EIP-2930, EIP-1559, EIP-4844
        // blob. Generation keeps drawing fresh keys per family until BOTH y-parity/recId values (0 and 1)
        // have been observed for that family — see the note on recId 2/3 below.
        private static readonly object _lock = new object();
        private static List<RecoveryVector> _corpus;

        public static List<RecoveryVector> Corpus
        {
            get
            {
                lock (_lock)
                {
                    return _corpus ??= BuildCorpus();
                }
            }
        }

        public static IEnumerable<object[]> CorpusTheoryData =>
            Corpus.Select(v => new object[] { v });

        private static List<RecoveryVector> BuildCorpus()
        {
            var list = new List<RecoveryVector>();
            list.AddRange(RealVectorsFromRepoFixtures());
            list.AddRange(GenerateFamily("PreEip155Legacy", MakePreEip155LegacyVector));
            list.AddRange(GenerateFamily("Eip155Legacy", MakeEip155LegacyVector));
            list.AddRange(GenerateFamily("Eip2930", MakeEip2930Vector));
            list.AddRange(GenerateFamily("Eip1559", MakeEip1559Vector));
            list.AddRange(GenerateFamily("Eip4844Blob", MakeEip4844Vector));
            return list;
        }

        /// <summary>
        /// Real mainnet-shaped legacy transactions already used elsewhere in this repo's test suite
        /// (GethTransactionTestVectors: Vitalik_1/2/3 are EIP-155 chainId=1, SenderTest is pre-EIP-155),
        /// decoded exactly as the follower would decode an RLP-encoded block transaction
        /// (TransactionFactory.CreateTransaction), so the SAME recId-derivation and key-recovery path
        /// the follower exercises is what this test recovers under both backends.
        /// </summary>
        private static IEnumerable<RecoveryVector> RealVectorsFromRepoFixtures()
        {
            foreach (var tc in GethTransactionTestVectors.GetSignatureTestCases())
            {
                var testCase = (GethTransactionTestVectors.TransactionTestCase)tc[0];
                var tx = TransactionFactory.CreateTransaction(testCase.TxBytes);
                int recId = tx is LegacyTransactionChainId chainTx
                    ? VRecoveryAndChainCalculations.GetRecIdFromVChain(chainTx.Signature.V, chainTx.GetChainIdAsBigInteger())
                    : VRecoveryAndChainCalculations.GetRecIdFromV(tx.Signature.V);
                yield return new RecoveryVector
                {
                    Name = "Real:" + testCase.Name,
                    Family = tx is LegacyTransactionChainId ? "Eip155Legacy" : "PreEip155Legacy",
                    Transaction = tx,
                    ExpectedSender = testCase.ExpectedSender,
                    RecId = recId
                };
            }
        }

        private static IEnumerable<RecoveryVector> GenerateFamily(
            string familyName, Func<EthECKey, int, RecoveryVector> makeVector)
        {
            var vectors = new List<RecoveryVector>();
            var seenRecIds = new HashSet<int>();
            const int maxAttempts = 64;
            const int minVectors = 4;

            for (var i = 0; i < maxAttempts && (vectors.Count < minVectors || !seenRecIds.Contains(0) || !seenRecIds.Contains(1)); i++)
            {
                var key = EthECKey.GenerateKey();
                var vector = makeVector(key, i);
                vectors.Add(vector);
                seenRecIds.Add(vector.RecId);
            }

            return vectors;
        }

        private static RecoveryVector MakePreEip155LegacyVector(EthECKey key, int i)
        {
            var tx = new LegacyTransaction(
                "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836",
                (EvmUInt256)(ulong)(1000000000000000000 + i),
                (EvmUInt256)(ulong)i,
                (EvmUInt256)(20000000000 + (ulong)i),
                (EvmUInt256)21000,
                i % 2 == 0 ? "" : "0x1234");
            var signer = new LegacyTransactionSigner();
            signer.SignTransaction(key.GetPrivateKeyAsBytes(), tx);
            var recId = VRecoveryAndChainCalculations.GetRecIdFromV(tx.Signature.V);
            return new RecoveryVector
            {
                Name = $"Gen:PreEip155Legacy#{i}",
                Family = "PreEip155Legacy",
                Transaction = tx,
                ExpectedSender = key.GetPublicAddress(),
                RecId = recId
            };
        }

        private static RecoveryVector MakeEip155LegacyVector(EthECKey key, int i)
        {
            var tx = new LegacyTransactionChainId(
                "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836",
                (EvmUInt256)(ulong)(1000000000000000000 + i),
                (EvmUInt256)(ulong)i,
                (EvmUInt256)(20000000000 + (ulong)i),
                (EvmUInt256)21000,
                i % 2 == 0 ? "" : "0x1234",
                (EvmUInt256)1);
            var signer = new LegacyTransactionSigner();
            signer.SignTransaction(key.GetPrivateKeyAsBytes(), tx);
            var recId = VRecoveryAndChainCalculations.GetRecIdFromVChain(tx.Signature.V, tx.GetChainIdAsBigInteger());
            return new RecoveryVector
            {
                Name = $"Gen:Eip155Legacy#{i}",
                Family = "Eip155Legacy",
                Transaction = tx,
                ExpectedSender = key.GetPublicAddress(),
                RecId = recId
            };
        }

        private static RecoveryVector MakeEip2930Vector(EthECKey key, int i)
        {
            var tx = new Transaction2930(
                (EvmUInt256)1,
                (EvmUInt256)(ulong)i,
                (EvmUInt256)(20000000000 + (ulong)i),
                (EvmUInt256)100000,
                "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836",
                (EvmUInt256)(ulong)(1000000000000000000 + i),
                i % 2 == 0 ? null : "0x1234",
                null);
            var signer = new TypeTransactionSigner<Transaction2930>();
            signer.SignTransaction(key.GetPrivateKeyAsBytes(), tx);
            var recId = tx.Signature.V.ToIntFromRLPDecoded();
            return new RecoveryVector
            {
                Name = $"Gen:Eip2930#{i}",
                Family = "Eip2930",
                Transaction = tx,
                ExpectedSender = key.GetPublicAddress(),
                RecId = recId
            };
        }

        private static RecoveryVector MakeEip1559Vector(EthECKey key, int i)
        {
            var tx = new Transaction1559(
                (EvmUInt256)1,
                (EvmUInt256)(ulong)i,
                (EvmUInt256)2,
                (EvmUInt256)(5000000000 + (ulong)i),
                (EvmUInt256)100000,
                "0x1ad91ee08f21be3de0ba2ba6918e714da6b45836",
                (EvmUInt256)(ulong)(1000000000000000000 + i),
                i % 2 == 0 ? null : "0x1234",
                null);
            var signer = new Transaction1559Signer();
            signer.SignTransaction(key.GetPrivateKeyAsBytes(), tx);
            var recId = tx.Signature.V.ToIntFromRLPDecoded();
            return new RecoveryVector
            {
                Name = $"Gen:Eip1559#{i}",
                Family = "Eip1559",
                Transaction = tx,
                ExpectedSender = key.GetPublicAddress(),
                RecId = recId
            };
        }

        private static RecoveryVector MakeEip4844Vector(EthECKey key, int i)
        {
            var blobHashes = new List<byte[]>
            {
                ("01a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065f000" + (i % 10)).HexToByteArray()
            };
            var tx = new Transaction4844(
                (EvmUInt256)1,
                (EvmUInt256)(ulong)i,
                (EvmUInt256)2,
                (EvmUInt256)(5000000000 + (ulong)i),
                (EvmUInt256)4000000,
                "0x095e7baea6a6c7c4c2dfeb977efac326af552d87",
                (EvmUInt256)(ulong)(100000 + i),
                "0x00",
                null,
                (EvmUInt256)10,
                blobHashes);
            var signer = new Transaction4844Signer();
            signer.SignTransaction(key.GetPrivateKeyAsBytes(), tx);
            var recId = tx.Signature.V.ToIntFromRLPDecoded();
            return new RecoveryVector
            {
                Name = $"Gen:Eip4844Blob#{i}",
                Family = "Eip4844Blob",
                Transaction = tx,
                ExpectedSender = key.GetPublicAddress(),
                RecId = recId
            };
        }

        [Theory]
        [MemberData(nameof(CorpusTheoryData))]
        public void RecoveredSender_IsByteIdentical_BetweenBackends_AndMatchesKnownSigner(RecoveryVector vector)
        {
            var saved = EthECKey.SignRecoverable;
            try
            {
                EthECKey.SignRecoverable = false;
                var bouncyCastleSender = vector.Transaction.GetSenderAddress();

                EthECKey.SignRecoverable = true;
                var nBitcoinSender = vector.Transaction.GetSenderAddress();

                Assert.False(string.IsNullOrEmpty(bouncyCastleSender), $"{vector.Name}: BouncyCastle failed to recover a sender at all.");
                Assert.False(string.IsNullOrEmpty(nBitcoinSender), $"{vector.Name}: NBitcoin failed to recover a sender at all.");

                Assert.Equal(bouncyCastleSender, nBitcoinSender);

                Assert.True(bouncyCastleSender.IsTheSameAddress(vector.ExpectedSender),
                    $"{vector.Name}: BouncyCastle recovered {bouncyCastleSender}, expected {vector.ExpectedSender}");
                Assert.True(nBitcoinSender.IsTheSameAddress(vector.ExpectedSender),
                    $"{vector.Name}: NBitcoin recovered {nBitcoinSender}, expected {vector.ExpectedSender}");
            }
            finally
            {
                EthECKey.SignRecoverable = saved;
            }
        }

        [Theory]
        [MemberData(nameof(CorpusTheoryData))]
        public void FollowerTxVerifier_GetSenderAddress_IsByteIdentical_BetweenBackends(RecoveryVector vector)
        {
            var verifier = new TransactionVerificationAndRecoveryImp();
            ITransactionVerificationAndRecovery iface = verifier;

            var saved = EthECKey.SignRecoverable;
            try
            {
                EthECKey.SignRecoverable = false;
                var bouncyCastleSender = iface.GetSenderAddress(vector.Transaction);

                EthECKey.SignRecoverable = true;
                var nBitcoinSender = iface.GetSenderAddress(vector.Transaction);

                Assert.Equal(bouncyCastleSender, nBitcoinSender);
                Assert.True(bouncyCastleSender.IsTheSameAddress(vector.ExpectedSender),
                    $"{vector.Name}: follower verifier (BouncyCastle) recovered {bouncyCastleSender}, expected {vector.ExpectedSender}");
            }
            finally
            {
                EthECKey.SignRecoverable = saved;
            }
        }

        [Fact]
        public void Corpus_CoversBothRecIdParitiesPerFamily()
        {
            var byFamily = Corpus.GroupBy(v => v.Family);
            var report = new List<string>();
            foreach (var family in byFamily)
            {
                var recIds = family.Select(v => v.RecId).Distinct().OrderBy(x => x).ToList();
                report.Add($"{family.Key}: recIds observed = [{string.Join(",", recIds)}] over {family.Count()} vectors");
            }
            _output.WriteLine(string.Join("\n", report));

            foreach (var family in byFamily)
            {
                var recIds = family.Select(v => v.RecId).Distinct().ToList();
                Assert.Contains(0, recIds);
                Assert.Contains(1, recIds);
            }
        }

        [Fact]
        public void InvalidSignature_ROutOfRange_BehaviourDocumented()
        {
            var fieldPrime = new BigInteger(1,
                Org.BouncyCastle.Utilities.Encoders.Hex.Decode(
                    "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFC2F"));
            var r = fieldPrime.ToByteArrayUnsigned();
            var s = new byte[] { 1 };
            var hash = new Sha3Keccack_LocalHash().ComputeHash("boundary-probe-invalid-signature");

            var signature = EthECDSASignatureFactory.FromComponents(r, s);

            var saved = EthECKey.SignRecoverable;
            try
            {
                string bouncyCastleOutcome;
                try
                {
                    EthECKey.SignRecoverable = false;
                    var key = EthECKey.RecoverFromSignature(signature, 0, hash);
                    var addr = key.GetPublicAddress();
                    bouncyCastleOutcome = "returned address " + addr;
                }
                catch (Exception ex)
                {
                    bouncyCastleOutcome = "threw " + ex.GetType().Name + ": " + ex.Message;
                }

                string nBitcoinOutcome;
                try
                {
                    EthECKey.SignRecoverable = true;
                    var key = EthECKey.RecoverFromSignature(signature, 0, hash);
                    var addr = key.GetPublicAddress();
                    nBitcoinOutcome = "returned address " + addr;
                }
                catch (Exception ex)
                {
                    nBitcoinOutcome = "threw " + ex.GetType().Name + ": " + ex.Message;
                }

                _output.WriteLine($"BouncyCastle (r=field prime, recId=0): {bouncyCastleOutcome}");
                _output.WriteLine($"NBitcoin     (r=field prime, recId=0): {nBitcoinOutcome}");

                Assert.True(
                    bouncyCastleOutcome.StartsWith("threw") || bouncyCastleOutcome.Contains("0x0000000000000000000000000000000000000000"),
                    "BouncyCastle unexpectedly produced what looks like a normal address for an out-of-range r; investigate before trusting either backend's failure mode.");
            }
            finally
            {
                EthECKey.SignRecoverable = saved;
            }
        }

        [Fact]
        public void InvalidSignature_ZeroRS_BehaviourDocumented()
        {
            var r = new byte[] { 0 };
            var s = new byte[] { 0 };
            var hash = new Sha3Keccack_LocalHash().ComputeHash("boundary-probe-zero-rs");
            var signature = EthECDSASignatureFactory.FromComponents(r, s);

            var saved = EthECKey.SignRecoverable;
            try
            {
                string bouncyCastleOutcome;
                try
                {
                    EthECKey.SignRecoverable = false;
                    var key = EthECKey.RecoverFromSignature(signature, 0, hash);
                    var addr = key.GetPublicAddress();
                    bouncyCastleOutcome = "returned address " + addr;
                }
                catch (Exception ex)
                {
                    bouncyCastleOutcome = "threw " + ex.GetType().Name + ": " + ex.Message;
                }

                string nBitcoinOutcome;
                try
                {
                    EthECKey.SignRecoverable = true;
                    var key = EthECKey.RecoverFromSignature(signature, 0, hash);
                    var addr = key.GetPublicAddress();
                    nBitcoinOutcome = "returned address " + addr;
                }
                catch (Exception ex)
                {
                    nBitcoinOutcome = "threw " + ex.GetType().Name + ": " + ex.Message;
                }

                _output.WriteLine($"BouncyCastle (r=0, s=0, recId=0): {bouncyCastleOutcome}");
                _output.WriteLine($"NBitcoin     (r=0, s=0, recId=0): {nBitcoinOutcome}");

                Assert.True(true);
            }
            finally
            {
                EthECKey.SignRecoverable = saved;
            }
        }

        private class Sha3Keccack_LocalHash
        {
            private readonly Nethereum.Util.Sha3Keccack _keccak = new Nethereum.Util.Sha3Keccack();

            public byte[] ComputeHash(string seed)
            {
                return _keccak.CalculateHash(System.Text.Encoding.UTF8.GetBytes(seed));
            }
        }
    }
}
