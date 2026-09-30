using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nethereum.EVM;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests
{
    public class BinaryBlockWitnessRoundTripTests
    {
        private readonly ITestOutputHelper _output;

        public BinaryBlockWitnessRoundTripTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static BlockWitnessData FullyPopulated()
        {
            return new BlockWitnessData
            {
                BlockNumber = 21_000_001,
                Timestamp = 1_700_000_123,
                BaseFee = 7,
                BlockGasLimit = 30_000_000,
                ChainId = 1,
                Coinbase = "0x2adc25665018aa1fe0e6bc666dac8fc2697ff9ba",
                Difficulty = new byte[32],
                ParentHash = Enumerable.Repeat((byte)0x11, 32).ToArray(),
                ExtraData = new byte[] { 0xde, 0xad },
                MixHash = Enumerable.Repeat((byte)0x22, 32).ToArray(),
                Nonce = Enumerable.Repeat((byte)0x33, 8).ToArray(),

                PreStateRoot = Enumerable.Repeat((byte)0x44, 32).ToArray(),
                ParentBeaconBlockRoot = Enumerable.Repeat((byte)0x55, 32).ToArray(),
                RequestsHash = Enumerable.Repeat((byte)0x66, 32).ToArray(),
                BlobGasUsed = 131_072,
                ExcessBlobGas = 262_144,
                SlotNumber = 9_876_543,
                Withdrawals = new List<BlockWithdrawal>
                {
                    new BlockWithdrawal
                    {
                        Index = 42, ValidatorIndex = 7,
                        Address = "0x1000000000000000000000000000000000000001",
                        AmountInGwei = 32_000_000_000
                    }
                },

                Features = new BlockFeatureConfig
                {
                    Fork = HardforkName.Prague
                },
                Transactions = new List<BlockWitnessTransaction> { Type4Transaction(null, RecoveredAuthority) },
                Accounts = new List<WitnessAccount>()
            };
        }

        private const string RecoveredAuthority = "0x7099797f051fa4a8ac7d68d2a4b2f0a1a1b0f4b1";

        private const string GoldenOrdinaryNonceWitness =
            "030013416f4001000000007bf1536500000000070000000000000080c3c9010000000001000000000000002a0030783261646332353636353031386161316665306536626336363664616338666332363937666639626100000000000000000000000000000000000000000000000000000000000000004444444444444444444444444444444444444444444444444444444444444444111111111111111111111111111111111111111111111111111111111111111102000000dead222222222222222222222222222222222222222222222222222222222222222233333333333333330101002a0000000000000007000000000000002a003078313030303030303030303030303030303030303030303030303030303030303030303030303030310040597307000000010000020000000000010000040000000000015555555555555555555555555555555555555555555555555555555555555555016666666666666666666666666666666666666666666666666666666666666666013fb496000000000001002a003078663339666436653531616164383866366634636536616238383237323739636666666239323236360200000004c001020000012a0030783730393937393766303531666134613861633764363864326134623266306131613162306634623101002a00307831303030303030303030303030303030303030303030303030303030303030303030303030303032000000000000000000000000000000000000000000000000000000000000007b0700000000000000000000000000000000000000";

        private const string GoldenMaximumSignedNonceWitness =
            "030013416f4001000000007bf1536500000000070000000000000080c3c9010000000001000000000000002a0030783261646332353636353031386161316665306536626336363664616338666332363937666639626100000000000000000000000000000000000000000000000000000000000000004444444444444444444444444444444444444444444444444444444444444444111111111111111111111111111111111111111111111111111111111111111102000000dead222222222222222222222222222222222222222222222222222222222222222233333333333333330101002a0000000000000007000000000000002a003078313030303030303030303030303030303030303030303030303030303030303030303030303030310040597307000000010000020000000000010000040000000000015555555555555555555555555555555555555555555555555555555555555555016666666666666666666666666666666666666666666666666666666666666666013fb496000000000001002a003078663339666436653531616164383866366634636536616238383237323739636666666239323236360200000004c001020000012a0030783730393937393766303531666134613861633764363864326134623266306131613162306634623101002a00307831303030303030303030303030303030303030303030303030303030303030303030303030303032000000000000000000000000000000000000000000000000000000000000007bffffffffffffff7f000000000000000000000000";

        private static BlockWitnessTransaction Type4Transaction(params string[] authorities)
        {
            return new BlockWitnessTransaction
            {
                From = "0xf39fd6e51aad88f6f4ce6ab8827279cfffb92266",
                RlpEncoded = new byte[] { 0x04, 0xc0 },
                AuthorisationAuthorities = new List<string>(authorities)
            };
        }

        private static BlockWitnessTransaction TransactionWithoutAuthorizations()
        {
            return new BlockWitnessTransaction
            {
                From = "0xf39fd6e51aad88f6f4ce6ab8827279cfffb92266",
                RlpEncoded = new byte[] { 0x02, 0xc0 }
            };
        }

        private static BlockWitnessData WitnessCarrying(params BlockWitnessTransaction[] transactions)
        {
            var witness = FullyPopulated();
            witness.Transactions = new List<BlockWitnessTransaction>(transactions);
            return witness;
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_ATransactionWitnessCarryingAuthorities_When_RoundTripped_Then_TheyArrivePositionally()
        {
            var second = "0x3c44cdddb6a900fa2b585dd299e03d12fa4293bc";

            var restored = BinaryBlockWitness.Deserialize(BinaryBlockWitness.Serialize(
                WitnessCarrying(Type4Transaction(RecoveredAuthority, second))));

            var authorities = Assert.Single(restored.Transactions).AuthorisationAuthorities;
            Assert.Equal(new[] { RecoveredAuthority, second }, authorities);
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_AnAuthorityTheHostCouldNotRecover_When_RoundTripped_Then_ItStaysNullRatherThanEmpty()
        {
            var restored = BinaryBlockWitness.Deserialize(BinaryBlockWitness.Serialize(
                WitnessCarrying(Type4Transaction(null, RecoveredAuthority))));

            var authorities = Assert.Single(restored.Transactions).AuthorisationAuthorities;
            Assert.Equal(2, authorities.Count);
            Assert.Null(authorities[0]);
            Assert.Equal(RecoveredAuthority, authorities[1]);
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_ABlockWithNoType4Transaction_When_RoundTripped_Then_TheAuthorityListStaysAbsent()
        {
            var restored = BinaryBlockWitness.Deserialize(BinaryBlockWitness.Serialize(
                WitnessCarrying(TransactionWithoutAuthorizations())));

            var transaction = Assert.Single(restored.Transactions);
            Assert.Null(transaction.AuthorisationAuthorities);
            Assert.Equal(new byte[] { 0x02, 0xc0 }, transaction.RlpEncoded);
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_AnEmptyAuthorityList_When_RoundTripped_Then_ItIsNotReadAsAbsent()
        {
            var restored = BinaryBlockWitness.Deserialize(BinaryBlockWitness.Serialize(
                WitnessCarrying(Type4Transaction())));

            var authorities = Assert.Single(restored.Transactions).AuthorisationAuthorities;
            Assert.NotNull(authorities);
            Assert.Empty(authorities);
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_AWitnessWrittenByThePreviousFormat_When_Read_Then_ItIsRefusedRatherThanReadWithoutAuthorities()
        {
            var bytes = BinaryBlockWitness.Serialize(WitnessCarrying(Type4Transaction(RecoveredAuthority)));
            bytes[0] = (byte)(BinaryBlockWitness.VERSION - 1);

            var refusal = Assert.Throws<System.InvalidOperationException>(
                () => BinaryBlockWitness.Deserialize(bytes));

            Assert.Contains(BinaryBlockWitness.VERSION.ToString(), refusal.Message);
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_AWitnessWithEveryFieldSet_When_RoundTripped_Then_NothingIsSilentlyDropped()
        {
            var original = FullyPopulated();

            var restored = BinaryBlockWitness.Deserialize(BinaryBlockWitness.Serialize(original));

            var dropped = new List<string>();
            foreach (var property in typeof(BlockWitnessData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead) continue;

                var before = property.GetValue(original);
                var after = property.GetValue(restored);

                if (before == null) continue;

                if (property.PropertyType == typeof(BlockFeatureConfig)) continue;

                if (!Equivalent(before, after))
                    dropped.Add($"{property.Name}: sent {Describe(before)}, got back {Describe(after)}");
            }

            foreach (var line in dropped) _output.WriteLine("  DROPPED " + line);

            Assert.True(dropped.Count == 0,
                $"{dropped.Count} field(s) did not survive serialisation. Each is a step the guest will " +
                $"silently skip while still producing a proof:" + System.Environment.NewLine +
                string.Join(System.Environment.NewLine, dropped));
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_AWitnessFeatureBlock_When_RoundTripped_Then_ItsFieldsSurvive()
        {
            var original = FullyPopulated();
            original.Features.StateTree = WitnessStateTreeType.Binary;
            original.Features.HashFunction = WitnessHashFunction.Blake3;

            var restored = BinaryBlockWitness.Deserialize(BinaryBlockWitness.Serialize(original));

            Assert.Equal(original.Features.Fork, restored.Features.Fork);
            Assert.Equal(original.Features.StateTree, restored.Features.StateTree);
            Assert.Equal(original.Features.HashFunction, restored.Features.HashFunction);
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_ZeroValuedBlockFields_When_RoundTripped_Then_TheyAreNotReadAsAbsent()
        {
            var original = FullyPopulated();
            original.ParentBeaconBlockRoot = new byte[32];
            original.RequestsHash = new byte[32];
            original.BlobGasUsed = 0;
            original.ExcessBlobGas = 0;
            original.SlotNumber = 0;
            original.Withdrawals = new List<BlockWithdrawal>();

            var restored = BinaryBlockWitness.Deserialize(BinaryBlockWitness.Serialize(original));

            Assert.NotNull(restored.ParentBeaconBlockRoot);
            Assert.NotNull(restored.RequestsHash);
            Assert.Equal(0, restored.BlobGasUsed);
            Assert.Equal(0, restored.ExcessBlobGas);
            Assert.Equal(0UL, restored.SlotNumber);
            Assert.NotNull(restored.Withdrawals);
            Assert.Empty(restored.Withdrawals);
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_UnsetBlockFields_When_RoundTripped_Then_TheyStayAbsent()
        {
            var original = FullyPopulated();
            original.ParentBeaconBlockRoot = null;
            original.RequestsHash = null;
            original.BlobGasUsed = null;
            original.ExcessBlobGas = null;
            original.SlotNumber = null;
            original.Withdrawals = null;

            var restored = BinaryBlockWitness.Deserialize(BinaryBlockWitness.Serialize(original));

            Assert.Null(restored.ParentBeaconBlockRoot);
            Assert.Null(restored.RequestsHash);
            Assert.Null(restored.BlobGasUsed);
            Assert.Null(restored.ExcessBlobGas);
            Assert.Null(restored.SlotNumber);
            Assert.Null(restored.Withdrawals);
        }

        private static BlockWitnessData WitnessCarryingAnAccountWithNonce(ulong nonce)
        {
            var witness = FullyPopulated();
            witness.Accounts = new List<WitnessAccount>
            {
                new WitnessAccount
                {
                    Address = "0x1000000000000000000000000000000000000002",
                    Balance = new EvmUInt256(123UL),
                    Nonce = nonce,
                    Code = new byte[0],
                    Storage = new List<WitnessStorageSlot>()
                }
            };
            return witness;
        }

        private static ulong RoundTrippedNonce(ulong nonce) =>
            Assert.Single(BinaryBlockWitness.Deserialize(
                BinaryBlockWitness.Serialize(WitnessCarryingAnAccountWithNonce(nonce))).Accounts).Nonce;

        /// <summary>
        /// EIP-2681: <i>"Limit account nonce to be between 0 and 2^64-1."</i>
        ///
        /// <para>The top of that range is a value the protocol reaches and the
        /// fixtures exercise, and it is the one value a signed 64-bit carrier
        /// cannot hold. A witness that narrows it does not report a loss: the
        /// nonce comes back as something else and the block executes against
        /// state that was never the sender's.</para>
        /// </summary>
        [Fact]
        [Trait("Category", "Witness")]
        public void Given_AnAccountWithTheMaximumNonce_When_TheWitnessIsRoundTripped_Then_TheNonceSurvives()
        {
            Assert.Equal(ulong.MaxValue, RoundTrippedNonce(ulong.MaxValue));
        }

        [Fact]
        [Trait("Category", "Witness")]
        public void Given_AnAccountWithAnOrdinaryNonce_When_TheWitnessIsRoundTripped_Then_TheNonceIsUnchanged()
        {
            Assert.Equal(7UL, RoundTrippedNonce(7UL));
        }

        [Theory]
        [Trait("Category", "Witness")]
        [InlineData(GoldenOrdinaryNonceWitness, 7UL)]
        [InlineData(GoldenMaximumSignedNonceWitness, (ulong)long.MaxValue)]
        public void Given_AWitnessWrittenBeforeTheNonceWasWidened_When_Read_Then_ItDecodesAndReEncodesToTheSameBytes(
            string goldenHex, ulong expectedNonce)
        {
            var golden = goldenHex.HexToByteArray();

            var restored = BinaryBlockWitness.Deserialize(golden);

            Assert.Equal(expectedNonce, Assert.Single(restored.Accounts).Nonce);
            Assert.Equal(golden.ToHex(), BinaryBlockWitness.Serialize(restored).ToHex());
        }

        /// <summary>
        /// EIP-7843: <i>"slotNumber is a uint64 in big endian encoding."</i>
        ///
        /// <para>The slot number has occupied eight unsigned bytes on the wire
        /// since this section was added, so carrying it at its full width moves
        /// no byte and the format stays at version 3 — which is load-bearing:
        /// <c>ZiskBinaryWitness</c> compares the version exactly, and a bump
        /// would make the compiled guest refuse every witness.</para>
        /// </summary>
        [Fact]
        [Trait("Category", "Witness")]
        public void Given_AWitnessCarryingTheMaximumSlotNumber_When_RoundTripped_Then_TheValueSurvivesAtVersionThree()
        {
            var original = FullyPopulated();
            original.SlotNumber = ulong.MaxValue;

            var serialized = BinaryBlockWitness.Serialize(original);

            Assert.Equal(BinaryBlockWitness.VERSION, serialized[0]);
            Assert.Equal((byte)3, serialized[0]);
            Assert.Equal(ulong.MaxValue, BinaryBlockWitness.Deserialize(serialized).SlotNumber);
        }

        private static bool Equivalent(object before, object after)
        {
            if (after == null) return false;

            if (before is byte[] a && after is byte[] b)
                return a.Length == b.Length && a.SequenceEqual(b);

            if (before is List<BlockWitnessTransaction> ta && after is List<BlockWitnessTransaction> tb)
                return ta.Count == tb.Count && !ta.Where((tx, i) => !Equivalent(tx, tb[i])).Any();

            if (before is List<string> sa && after is List<string> sb)
                return sa.Count == sb.Count && !sa.Where((value, i) => value != sb[i]).Any();

            if (before is System.Collections.ICollection ca && after is System.Collections.ICollection cb)
                return ca.Count == cb.Count;

            return before.Equals(after);
        }

        private static bool Equivalent(BlockWitnessTransaction before, BlockWitnessTransaction after)
        {
            foreach (var property in typeof(BlockWitnessTransaction).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var sent = property.GetValue(before);
                if (sent == null) continue;
                if (!Equivalent(sent, property.GetValue(after))) return false;
            }
            return true;
        }

        private static string Describe(object value)
        {
            if (value == null) return "null";
            if (value is byte[] bytes) return "0x" + bytes.ToHex();
            if (value is System.Collections.ICollection c) return $"{c.Count} item(s)";
            return value.ToString();
        }
    }
}
