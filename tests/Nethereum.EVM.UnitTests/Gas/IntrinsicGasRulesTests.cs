using System.Collections.Generic;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Gas.Intrinsic;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class IntrinsicGasRulesTests
    {
        private const long TX_BASE = 21000;
        private const long TX_CREATE = 32000;
        private const long TX_DATA_ZERO = 4;
        private const long TX_DATA_NON_ZERO = 16;
        private const long INIT_CODE_WORD_GAS = 2;
        private const long ACCESS_LIST_ADDRESS = 2400;
        private const long ACCESS_LIST_STORAGE = 1900;
        private const long FLOOR_PER_TOKEN = 10;
        private const long TOKENS_PER_NONZERO = 4;


        [Theory]
        [InlineData(0,   false)]
        [InlineData(0,   true)]
        [InlineData(32,  false)]
        [InlineData(32,  true)]
        [InlineData(256, false)]
        [InlineData(256, true)]
        public void Cancun_intrinsic_matches_spec_formula_no_access_list(int dataLen, bool isCreation)
        {
            var data = BuildMixedData(dataLen);

            long expected = TX_BASE;
            if (isCreation)
            {
                expected += TX_CREATE;
                int words = (dataLen + 31) / 32;
                expected += (long)words * INIT_CODE_WORD_GAS;
            }
            expected += DataByteGas(data);

            long bundle = IntrinsicGasRuleSets.Cancun.CalculateIntrinsicGas(
                data, isCreation, accessList: null, isSelfTransfer: false, hasValue: false);

            Assert.Equal(expected, bundle);
        }

        [Theory]
        [InlineData(64, false)]
        [InlineData(64, true)]
        public void Cancun_intrinsic_matches_spec_formula_with_access_list(int dataLen, bool isCreation)
        {
            var data = BuildMixedData(dataLen);
            var accessList = BuildAccessList();

            long expected = TX_BASE;
            if (isCreation)
            {
                expected += TX_CREATE;
                int words = (dataLen + 31) / 32;
                expected += (long)words * INIT_CODE_WORD_GAS;
            }
            expected += DataByteGas(data);
            foreach (var entry in accessList)
            {
                expected += ACCESS_LIST_ADDRESS;
                if (entry.StorageKeys != null)
                    expected += entry.StorageKeys.Count * ACCESS_LIST_STORAGE;
            }

            long bundle = IntrinsicGasRuleSets.Cancun.CalculateIntrinsicGas(
                data, isCreation, accessList, isSelfTransfer: false, hasValue: false);

            Assert.Equal(expected, bundle);
        }


        [Theory]
        [InlineData(0,   false)]
        [InlineData(0,   true)]
        [InlineData(32,  false)]
        [InlineData(32,  true)]
        [InlineData(256, false)]
        [InlineData(256, true)]
        public void Prague_floor_matches_eip7623_formula(int dataLen, bool isCreation)
        {
            var data = BuildMixedData(dataLen);

            long tokens = Tokens(data);
            long expected = TX_BASE + FLOOR_PER_TOKEN * tokens;

            long bundle = IntrinsicGasRuleSets.Prague.CalculateFloorGasLimit(data, isCreation, isSelfTransfer: false, hasValue: false, accessList: null);

            Assert.Equal(expected, bundle);
        }

        public static IEnumerable<object[]> Eip7623FloorForks()
        {
            yield return new object[] { "Prague" };
            yield return new object[] { "Osaka" };
            yield return new object[] { "OsakaBpo1" };
        }

        private static IntrinsicGasRules Eip7623FloorBundle(string fork) => fork switch
        {
            "Prague" => IntrinsicGasRuleSets.Prague,
            "Osaka" => IntrinsicGasRuleSets.Osaka,
            "OsakaBpo1" => IntrinsicGasRuleSets.OsakaBpo1,
            _ => throw new System.ArgumentOutOfRangeException(nameof(fork), fork, null)
        };

        [Theory]
        [MemberData(nameof(Eip7623FloorForks))]
        public void Eip7623_creation_floor_excludes_txCreate_across_all_floor_forks(string fork)
        {
            var rules = Eip7623FloorBundle(fork);
            foreach (var dataLen in new[] { 0, 32, 256, 1775 })
            {
                var data = BuildMixedData(dataLen);
                long rawFloor = TX_BASE + FLOOR_PER_TOKEN * Tokens(data);

                long creationFloor = rules.CalculateFloorGasLimit(data, isContractCreation: true, isSelfTransfer: false, hasValue: false, accessList: null);
                long callFloor = rules.CalculateFloorGasLimit(data, isContractCreation: false, isSelfTransfer: false, hasValue: false, accessList: null);

                Assert.Equal(rawFloor, callFloor);
                Assert.Equal(rawFloor, creationFloor);
                Assert.NotEqual(rawFloor + TX_CREATE, creationFloor);
            }
        }

        [Fact]
        public void Cancun_floor_returns_zero_because_no_rule_installed()
        {
            var data = BuildMixedData(64);
            Assert.Equal(0L, IntrinsicGasRuleSets.Cancun.CalculateFloorGasLimit(data, isContractCreation: false, isSelfTransfer: false, hasValue: false, accessList: null));
            Assert.Equal(0L, IntrinsicGasRuleSets.Cancun.CalculateFloorGasLimit(data, isContractCreation: true, isSelfTransfer: false, hasValue: false, accessList: null));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(32)]
        [InlineData(256)]
        public void Prague_finalisation_floor_is_raw_tokens_formula(int dataLen)
        {
            var data = BuildMixedData(dataLen);

            long expected = TX_BASE + FLOOR_PER_TOKEN * Tokens(data);
            long bundle = IntrinsicGasRuleSets.Prague.CalculateFloorGasLimit(data, isContractCreation: false, isSelfTransfer: false, hasValue: false, accessList: null);

            Assert.Equal(expected, bundle);
        }


        private const long AMS_TX_BASE = 12000;
        private const long AMS_CREATE_ACCESS = 12000;
        private const long AMS_COLD_ACCOUNT_ACCESS = 3000;
        private const long AMS_TX_VALUE_COST = 6000;

        [Fact]
        public void Given_SimpleTransfer_When_IntrinsicComputed_Then_Equals12000PlusColdAccessPlusValue()
        {
            long expected = AMS_TX_BASE + AMS_COLD_ACCOUNT_ACCESS + AMS_TX_VALUE_COST;
            Assert.Equal(21000L, expected);

            long actual = IntrinsicGasRuleSets.Amsterdam.CalculateIntrinsicGas(
                data: null, isContractCreation: false, accessList: null,
                isSelfTransfer: false, hasValue: true);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Given_CreationVsCall_When_IntrinsicComputed_Then_RecipientCostDiffers()
        {
            long callGas = IntrinsicGasRuleSets.Amsterdam.CalculateIntrinsicGas(
                data: null, isContractCreation: false, accessList: null,
                isSelfTransfer: false, hasValue: false);

            long creationGas = IntrinsicGasRuleSets.Amsterdam.CalculateIntrinsicGas(
                data: null, isContractCreation: true, accessList: null,
                isSelfTransfer: false, hasValue: false);

            Assert.Equal(AMS_TX_BASE + AMS_COLD_ACCOUNT_ACCESS, callGas);
            Assert.Equal(AMS_TX_BASE + AMS_CREATE_ACCESS, creationGas);
            Assert.NotEqual(callGas, creationGas);
        }

        [Fact]
        public void Given_ValueBearingTx_When_NotSelfTransfer_Then_ChargesValueCost()
        {
            long withoutValue = IntrinsicGasRuleSets.Amsterdam.CalculateIntrinsicGas(
                data: null, isContractCreation: false, accessList: null,
                isSelfTransfer: false, hasValue: false);

            long withValue = IntrinsicGasRuleSets.Amsterdam.CalculateIntrinsicGas(
                data: null, isContractCreation: false, accessList: null,
                isSelfTransfer: false, hasValue: true);

            Assert.Equal(AMS_TX_VALUE_COST, withValue - withoutValue);
        }

        [Fact]
        public void Given_SelfTransfer_When_IntrinsicComputed_Then_NoRecipientOrValueCost()
        {
            long selfTransferWithValue = IntrinsicGasRuleSets.Amsterdam.CalculateIntrinsicGas(
                data: null, isContractCreation: false, accessList: null,
                isSelfTransfer: true, hasValue: true);

            Assert.Equal(AMS_TX_BASE, selfTransferWithValue);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void Given_FloorBindingTx_When_Settled_Then_FloorIncludesDecomposedBase(bool isContractCreation, bool isSelfTransfer)
        {
            var data = BuildMixedData(256);

            long recipient = isContractCreation
                ? AMS_CREATE_ACCESS
                : (isSelfTransfer ? 0 : AMS_COLD_ACCOUNT_ACCESS);
            long expectedAnchor = AMS_TX_BASE + recipient;

            long expected = expectedAnchor + AMS_FLOOR_PER_TOKEN * AMS_FLOOR_TOKENS_PER_BYTE * data.Length;

            long floor = IntrinsicGasRuleSets.Amsterdam.CalculateFloorGasLimit(
                data, isContractCreation, isSelfTransfer, hasValue: false, accessList: null);

            Assert.Equal(expected, floor);
            Assert.NotEqual(TX_BASE + FLOOR_PER_TOKEN * Tokens(data), floor);
        }


        [Fact]
        public void Cancun_blob_base_fee_at_zero_excess_is_minimum()
        {
            var fee = IntrinsicGasRuleSets.Cancun.Blob.CalculateBlobBaseFee(EvmUInt256.Zero);
            Assert.Equal(EvmUInt256.One, fee);
        }

        [Fact]
        public void Cancun_blob_base_fee_monotonically_increases_for_large_excess()
        {
            var low  = IntrinsicGasRuleSets.Cancun.Blob.CalculateBlobBaseFee(new EvmUInt256(3_338_477UL));
            var mid  = IntrinsicGasRuleSets.Cancun.Blob.CalculateBlobBaseFee(new EvmUInt256(6_676_954UL));
            var high = IntrinsicGasRuleSets.Cancun.Blob.CalculateBlobBaseFee(new EvmUInt256(13_353_908UL));

            Assert.True(low < mid, $"low {low} should be < mid {mid}");
            Assert.True(mid < high, $"mid {mid} should be < high {high}");
        }

        [Theory]
        [InlineData(0,  42UL)]
        [InlineData(1,  42UL)]
        [InlineData(6,  42UL)]
        public void Cancun_blob_gas_cost_equals_count_times_gas_per_blob_times_base_fee(int blobCount, ulong baseFee)
        {
            const int GAS_PER_BLOB = 131072;
            var expected = new EvmUInt256((ulong)blobCount * GAS_PER_BLOB) * new EvmUInt256(baseFee);
            var bundle = IntrinsicGasRuleSets.Cancun.Blob.CalculateBlobGasCost(blobCount, new EvmUInt256(baseFee));
            Assert.Equal(expected, bundle);
        }


        [Fact]
        public void Cancun_has_rules_installed_for_init_access_blob_but_no_floor()
        {
            Assert.NotNull(IntrinsicGasRuleSets.Cancun.InitCode);
            Assert.NotNull(IntrinsicGasRuleSets.Cancun.AccessList);
            Assert.NotNull(IntrinsicGasRuleSets.Cancun.Blob);
            Assert.Null(IntrinsicGasRuleSets.Cancun.Floor);
        }

        [Fact]
        public void Prague_adds_floor_but_reuses_cancun_slots_by_reference()
        {
            Assert.Same(IntrinsicGasRuleSets.Cancun.InitCode,   IntrinsicGasRuleSets.Prague.InitCode);
            Assert.Same(IntrinsicGasRuleSets.Cancun.AccessList, IntrinsicGasRuleSets.Prague.AccessList);

            Assert.Same(Eip4844BlobGasRule.Instance,  IntrinsicGasRuleSets.Cancun.Blob);
            Assert.Same(Eip7691BlobGasRule.Instance,  IntrinsicGasRuleSets.Prague.Blob);

            Assert.NotNull(IntrinsicGasRuleSets.Prague.Floor);
            Assert.Null(IntrinsicGasRuleSets.Cancun.Floor);
        }

        [Fact]
        public void Osaka_reuses_prague_non_blob_slots_by_reference()
        {
            Assert.Same(IntrinsicGasRuleSets.Prague.InitCode,   IntrinsicGasRuleSets.Osaka.InitCode);
            Assert.Same(IntrinsicGasRuleSets.Prague.AccessList, IntrinsicGasRuleSets.Osaka.AccessList);
            Assert.Same(IntrinsicGasRuleSets.Prague.Floor,      IntrinsicGasRuleSets.Osaka.Floor);

            Assert.Same(Eip7691BlobGasRule.Instance, IntrinsicGasRuleSets.Prague.Blob);
            Assert.Same(Eip7892BlobGasRule.Instance, IntrinsicGasRuleSets.Osaka.Blob);
        }


        [Theory]
        [InlineData(0,   0)]
        [InlineData(1,   2)]
        [InlineData(32,  2)]
        [InlineData(33,  4)]
        [InlineData(64,  4)]
        [InlineData(65,  6)]
        public void Eip3860_initcode_word_gas(int codeLen, long expected)
        {
            var code = new byte[codeLen];
            Assert.Equal(expected, Eip3860InitCodeGasRule.Instance.CalculateGas(code));
        }

        [Fact]
        public void Eip3860_initcode_word_gas_returns_zero_for_null()
        {
            Assert.Equal(0L, Eip3860InitCodeGasRule.Instance.CalculateGas(null));
        }

        [Fact]
        public void Eip2930_access_list_gas_matches_spec_formula()
        {
            var list = BuildAccessList();
            long expected = 0;
            foreach (var e in list)
            {
                expected += ACCESS_LIST_ADDRESS;
                if (e.StorageKeys != null)
                    expected += e.StorageKeys.Count * ACCESS_LIST_STORAGE;
            }
            Assert.Equal(expected, Eip2930AccessListGasRule.Instance.CalculateGas(list));
        }

        [Fact]
        public void Eip2930_access_list_gas_returns_zero_for_null()
        {
            Assert.Equal(0L, Eip2930AccessListGasRule.Instance.CalculateGas(null));
        }

        [Fact]
        public void Eip7623_tokens_and_floor_follow_spec_formula()
        {
            var data = BuildMixedData(128);
            long tokens = Tokens(data);

            Assert.Equal(tokens, Eip7623CalldataFloorRule.Instance.FloorTokensInCalldata(data));
            Assert.Equal(FLOOR_PER_TOKEN * tokens,
                Eip7623CalldataFloorRule.Instance.FloorPerTokenGas(data));

            Assert.Equal(TX_BASE + FLOOR_PER_TOKEN * tokens,
                IntrinsicGasRuleSets.Prague.CalculateFloorGasLimit(data,
                    isContractCreation: false, isSelfTransfer: false, hasValue: false, accessList: null));
        }

        [Fact]
        public void Null_floor_rule_returns_zero_via_bundle_even_for_long_data()
        {
            var data = BuildMixedData(1024);
            Assert.Equal(0L, IntrinsicGasRuleSets.Cancun.CalculateFloorGasLimit(data, isContractCreation: false, isSelfTransfer: false, hasValue: false, accessList: null));
        }


        private const long AMS_FLOOR_PER_TOKEN = 16;
        private const long AMS_FLOOR_TOKENS_PER_BYTE = 4;

        [Theory]
        [InlineData(0)]
        [InlineData(32)]
        [InlineData(256)]
        public void Amsterdam_floor_tokens_are_uniform_per_byte_not_zero_weighted(int dataLen)
        {
            var data = BuildMixedData(dataLen);

            Assert.Equal((long)dataLen * AMS_FLOOR_TOKENS_PER_BYTE,
                Eip7976CalldataFloorRule.Instance.FloorTokensInCalldata(data));
            Assert.Equal((long)dataLen * AMS_FLOOR_TOKENS_PER_BYTE * AMS_FLOOR_PER_TOKEN,
                Eip7976CalldataFloorRule.Instance.FloorPerTokenGas(data));

            Assert.Equal((long)dataLen * 64, Eip7976CalldataFloorRule.Instance.FloorPerTokenGas(data));
        }

        [Theory]
        [InlineData(32)]
        [InlineData(256)]
        public void Amsterdam_floor_tokens_differ_from_eip7623_whenever_data_has_zero_bytes(int dataLen)
        {
            var data = BuildMixedData(dataLen);

            Assert.Equal(Tokens(data), Eip7623CalldataFloorRule.Instance.FloorTokensInCalldata(data));
            Assert.NotEqual(Eip7623CalldataFloorRule.Instance.FloorTokensInCalldata(data),
                Eip7976CalldataFloorRule.Instance.FloorTokensInCalldata(data));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(32)]
        [InlineData(256)]
        public void Amsterdam_bundle_floor_anchors_on_decomposed_base_plus_uniform_tokens(int dataLen)
        {
            var data = BuildMixedData(dataLen);

            long expected = AMS_TX_BASE + AMS_COLD_ACCOUNT_ACCESS + (long)dataLen * 64;

            Assert.Equal(expected, IntrinsicGasRuleSets.Amsterdam.CalculateFloorGasLimit(
                data, isContractCreation: false, isSelfTransfer: false, hasValue: false, accessList: null));
        }


        private const long AMS_ACCESS_LIST_ADDRESS_DERIVED = 2900;
        private const long AMS_ACCESS_LIST_STORAGE_DERIVED = 2000;
        private const long AMS_ACCESS_LIST_ADDRESS_FLOOR_TOKENS = 80;
        private const long AMS_ACCESS_LIST_STORAGE_FLOOR_TOKENS = 128;

        [Fact]
        public void Given_AccessListEntries_AtAmsterdam_When_FloorComputed_Then_AddsExpectedTokens()
        {
            var accessList = BuildAccessList();
            long expectedTokens = 2 * AMS_ACCESS_LIST_ADDRESS_FLOOR_TOKENS + 2 * AMS_ACCESS_LIST_STORAGE_FLOOR_TOKENS;

            Assert.Equal(expectedTokens, Eip7981AccessListGasRule.Instance.FloorTokensInAccessList(accessList));
            Assert.Equal(expectedTokens * AMS_FLOOR_PER_TOKEN, Eip7981AccessListGasRule.Instance.FloorPerTokenGas(accessList));

            var data = BuildMixedData(32);
            long expectedFloor = AMS_TX_BASE + AMS_COLD_ACCOUNT_ACCESS
                + (long)data.Length * AMS_FLOOR_TOKENS_PER_BYTE * AMS_FLOOR_PER_TOKEN
                + expectedTokens * AMS_FLOOR_PER_TOKEN;

            long actualFloor = IntrinsicGasRuleSets.Amsterdam.CalculateFloorGasLimit(
                data, isContractCreation: false, isSelfTransfer: false, hasValue: false, accessList);

            Assert.Equal(expectedFloor, actualFloor);
        }

        [Fact]
        public void Given_AccessListEntries_AtOsaka_When_FloorComputed_Then_NoSurcharge_NoCrossForkLeak()
        {
            Assert.Null(IntrinsicGasRuleSets.Osaka.AccessListFloor);

            var accessList = BuildAccessList();
            var data = BuildMixedData(32);

            long floorWithList = IntrinsicGasRuleSets.Osaka.CalculateFloorGasLimit(
                data, isContractCreation: false, isSelfTransfer: false, hasValue: false, accessList);
            long floorWithoutList = IntrinsicGasRuleSets.Osaka.CalculateFloorGasLimit(
                data, isContractCreation: false, isSelfTransfer: false, hasValue: false, accessList: null);

            Assert.Equal(floorWithoutList, floorWithList);
        }

        [Fact]
        public void Given_AccessListEntry_AtAmsterdam_When_IntrinsicComputed_Then_Charges2900Not2400()
        {
            var accessList = BuildAccessList();
            long expected = 2 * (AMS_ACCESS_LIST_ADDRESS_DERIVED + AMS_ACCESS_LIST_ADDRESS_FLOOR_TOKENS * AMS_FLOOR_PER_TOKEN)
                + 2 * (AMS_ACCESS_LIST_STORAGE_DERIVED + AMS_ACCESS_LIST_STORAGE_FLOOR_TOKENS * AMS_FLOOR_PER_TOKEN);

            long actual = Eip7981AccessListGasRule.Instance.CalculateGas(accessList);

            Assert.Equal(4180, AMS_ACCESS_LIST_ADDRESS_DERIVED + AMS_ACCESS_LIST_ADDRESS_FLOOR_TOKENS * AMS_FLOOR_PER_TOKEN);
            Assert.Equal(4048, AMS_ACCESS_LIST_STORAGE_DERIVED + AMS_ACCESS_LIST_STORAGE_FLOOR_TOKENS * AMS_FLOOR_PER_TOKEN);
            Assert.Equal(expected, actual);

            long staleEip2930Total = accessList.Count * ACCESS_LIST_ADDRESS + 2 * ACCESS_LIST_STORAGE;
            Assert.NotEqual(staleEip2930Total, actual);
        }

        [Fact]
        public void Given_AccessListEntry_AtOsaka_When_IntrinsicComputed_Then_StaysAt2400Not2900_NoCrossForkLeak()
        {
            Assert.Same(Eip2930AccessListGasRule.Instance, IntrinsicGasRuleSets.Osaka.AccessList);

            var accessList = BuildAccessList();
            long expected = accessList.Count * ACCESS_LIST_ADDRESS + 2 * ACCESS_LIST_STORAGE;

            long actual = IntrinsicGasRuleSets.Osaka.AccessList.CalculateGas(accessList);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Given_AccessListTx_AtAmsterdam_When_ExecutionBranchBinds_Then_SurchargeStillPaid()
        {
            var accessList = BuildAccessList();
            var data = System.Array.Empty<byte>();

            long intrinsicWithList = IntrinsicGasRuleSets.Amsterdam.CalculateIntrinsicGas(
                data, isContractCreation: false, accessList, isSelfTransfer: false, hasValue: false);
            long floorWithList = IntrinsicGasRuleSets.Amsterdam.CalculateFloorGasLimit(
                data, isContractCreation: false, isSelfTransfer: false, hasValue: false, accessList);

            Assert.True(intrinsicWithList > floorWithList,
                $"test precondition: execution branch must bind (intrinsic={intrinsicWithList}, floor={floorWithList})");

            long minimumWithList = IntrinsicGasRuleSets.Amsterdam.CalculateMinimumGasLimit(
                data, isContractCreation: false, accessList, isSelfTransfer: false, hasValue: false);
            long minimumWithoutList = IntrinsicGasRuleSets.Amsterdam.CalculateMinimumGasLimit(
                data, isContractCreation: false, accessList: null, isSelfTransfer: false, hasValue: false);

            Assert.Equal(intrinsicWithList, minimumWithList);

            long expectedDelta = 2 * (AMS_ACCESS_LIST_ADDRESS_DERIVED + AMS_ACCESS_LIST_ADDRESS_FLOOR_TOKENS * AMS_FLOOR_PER_TOKEN)
                + 2 * (AMS_ACCESS_LIST_STORAGE_DERIVED + AMS_ACCESS_LIST_STORAGE_FLOOR_TOKENS * AMS_FLOOR_PER_TOKEN);
            Assert.Equal(expectedDelta, minimumWithList - minimumWithoutList);
        }

        [Fact]
        public void Given_AccessListTx_AtOsaka_When_ExecutionBranchBinds_Then_NoSurcharge_NoCrossForkLeak()
        {
            var accessList = BuildAccessList();
            var data = System.Array.Empty<byte>();

            long minimumWithList = IntrinsicGasRuleSets.Osaka.CalculateMinimumGasLimit(
                data, isContractCreation: false, accessList, isSelfTransfer: false, hasValue: false);
            long minimumWithoutList = IntrinsicGasRuleSets.Osaka.CalculateMinimumGasLimit(
                data, isContractCreation: false, accessList: null, isSelfTransfer: false, hasValue: false);

            long expectedDelta = accessList.Count * ACCESS_LIST_ADDRESS + 2 * ACCESS_LIST_STORAGE;
            Assert.Equal(expectedDelta, minimumWithList - minimumWithoutList);
        }


        private static byte[] BuildMixedData(int length)
        {
            var data = new byte[length];
            for (int i = 0; i < length; i++)
                data[i] = (byte)(i % 2 == 0 ? 0 : 0xAB);
            return data;
        }

        private static long DataByteGas(byte[] data)
        {
            if (data == null) return 0;
            long gas = 0;
            foreach (var b in data)
                gas += b == 0 ? TX_DATA_ZERO : TX_DATA_NON_ZERO;
            return gas;
        }

        private static long Tokens(byte[] data)
        {
            if (data == null) return 0;
            int zeroBytes = 0, nonZeroBytes = 0;
            foreach (var b in data)
            {
                if (b == 0) zeroBytes++;
                else nonZeroBytes++;
            }
            return zeroBytes + nonZeroBytes * TOKENS_PER_NONZERO;
        }

        private static List<AccessListEntry> BuildAccessList()
        {
            return new List<AccessListEntry>
            {
                new AccessListEntry
                {
                    Address = "0x0000000000000000000000000000000000000001",
                    StorageKeys = new List<string>
                    {
                        "0x0000000000000000000000000000000000000000000000000000000000000000",
                        "0x0000000000000000000000000000000000000000000000000000000000000001",
                    }
                },
                new AccessListEntry
                {
                    Address = "0x0000000000000000000000000000000000000002",
                    StorageKeys = new List<string>()
                }
            };
        }
    }
}
