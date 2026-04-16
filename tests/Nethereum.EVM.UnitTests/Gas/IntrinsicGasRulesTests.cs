using System.Collections.Generic;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Gas.Intrinsic;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    /// <summary>
    /// Spec-formula regression tests for the composable
    /// <see cref="IntrinsicGasRules"/> bundles and their underlying
    /// rule implementations. Originally written as parity tests against
    /// the legacy <c>IntrinsicGasCalculator</c>; once that class was
    /// deleted in Step 5.7 the expected values were pinned to the
    /// EIP spec formulas directly so the tests remain meaningful.
    ///
    /// These assertions follow the same spec-formula shape as the
    /// precompile gas calculator tests landed in Step 5.6 — every
    /// expected value is either a literal from an EIP, a formula
    /// re-evaluated inline from the EIP text, or a value captured
    /// from the production-validated rule implementation running on
    /// a deterministic fake-exponential input.
    /// </summary>
    public class IntrinsicGasRulesTests
    {
        // EIP-specified constants (mirror the rule class implementations).
        private const long TX_BASE = 21000;
        private const long TX_CREATE = 32000;
        private const long TX_DATA_ZERO = 4;
        private const long TX_DATA_NON_ZERO = 16;
        private const long INIT_CODE_WORD_GAS = 2;
        private const long ACCESS_LIST_ADDRESS = 2400;
        private const long ACCESS_LIST_STORAGE = 1900;
        private const long FLOOR_PER_TOKEN = 10;
        private const long TOKENS_PER_NONZERO = 4;

        // -----------------------------------------------------------
        // CalculateIntrinsicGas — Cancun (init code + access list + no floor)
        // -----------------------------------------------------------

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
                data, isCreation, accessList: null);

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
                data, isCreation, accessList);

            Assert.Equal(expected, bundle);
        }

        // -----------------------------------------------------------
        // CalculateFloorGasLimit — Prague (EIP-7623)
        // -----------------------------------------------------------

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
            if (isCreation)
                expected += TX_CREATE;

            long bundle = IntrinsicGasRuleSets.Prague.CalculateFloorGasLimit(data, isCreation);

            Assert.Equal(expected, bundle);
        }

        [Fact]
        public void Cancun_floor_returns_zero_because_no_rule_installed()
        {
            var data = BuildMixedData(64);
            Assert.Equal(0L, IntrinsicGasRuleSets.Cancun.CalculateFloorGasLimit(data, isContractCreation: false));
            Assert.Equal(0L, IntrinsicGasRuleSets.Cancun.CalculateFloorGasLimit(data, isContractCreation: true));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(32)]
        [InlineData(256)]
        public void Prague_finalisation_floor_is_raw_tokens_formula(int dataLen)
        {
            // Finalisation path always passes isContractCreation: false
            // regardless of the actual tx type — the raw floor excludes
            // the contract-creation adder.
            var data = BuildMixedData(dataLen);

            long expected = TX_BASE + FLOOR_PER_TOKEN * Tokens(data);
            long bundle = IntrinsicGasRuleSets.Prague.CalculateFloorGasLimit(data, isContractCreation: false);

            Assert.Equal(expected, bundle);
        }

        // -----------------------------------------------------------
        // Blob gas (EIP-4844)
        // -----------------------------------------------------------

        [Fact]
        public void Cancun_blob_base_fee_at_zero_excess_is_minimum()
        {
            // fake_exponential(1, 0, 3338477) collapses to floor(1) = 1.
            var fee = IntrinsicGasRuleSets.Cancun.Blob.CalculateBlobBaseFee(EvmUInt256.Zero);
            Assert.Equal(EvmUInt256.One, fee);
        }

        [Fact]
        public void Cancun_blob_base_fee_monotonically_increases_for_large_excess()
        {
            // The fake_exponential approximation is bounded below by 1 (the
            // minimum base fee). It only grows above 1 once
            // excess > BLOB_BASE_FEE_UPDATE_FRACTION × ln(2) ≈ 2_313_985 —
            // so the three test points are spaced one fraction-width apart,
            // each clearly past the floor.
            var low  = IntrinsicGasRuleSets.Cancun.Blob.CalculateBlobBaseFee(new EvmUInt256(3_338_477UL));   // ~e^1
            var mid  = IntrinsicGasRuleSets.Cancun.Blob.CalculateBlobBaseFee(new EvmUInt256(6_676_954UL));   // ~e^2
            var high = IntrinsicGasRuleSets.Cancun.Blob.CalculateBlobBaseFee(new EvmUInt256(13_353_908UL));  // ~e^4

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

        // -----------------------------------------------------------
        // Composition identity — Prague reuses Cancun slots by reference
        // -----------------------------------------------------------

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
            // Prague = Cancun.WithFloor(...) so every other slot is the
            // same reference — no cloning waste.
            Assert.Same(IntrinsicGasRuleSets.Cancun.InitCode,   IntrinsicGasRuleSets.Prague.InitCode);
            Assert.Same(IntrinsicGasRuleSets.Cancun.AccessList, IntrinsicGasRuleSets.Prague.AccessList);
            Assert.Same(IntrinsicGasRuleSets.Cancun.Blob,       IntrinsicGasRuleSets.Prague.Blob);

            Assert.NotNull(IntrinsicGasRuleSets.Prague.Floor);
            Assert.Null(IntrinsicGasRuleSets.Cancun.Floor);
        }

        [Fact]
        public void Osaka_is_same_reference_as_prague_no_intrinsic_changes()
        {
            // No intrinsic tx gas changes in Osaka — the static field is
            // literally the Prague reference.
            Assert.Same(IntrinsicGasRuleSets.Prague, IntrinsicGasRuleSets.Osaka);
        }

        // -----------------------------------------------------------
        // Rule implementations in isolation
        // -----------------------------------------------------------

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

            Assert.Equal(tokens, Eip7623CalldataFloorRule.Instance.TokensInCalldata(data));
            Assert.Equal(TX_BASE + FLOOR_PER_TOKEN * tokens,
                Eip7623CalldataFloorRule.Instance.CalculateFloor(data));
        }

        [Fact]
        public void Null_floor_rule_returns_zero_via_bundle_even_for_long_data()
        {
            var data = BuildMixedData(1024);
            Assert.Equal(0L, IntrinsicGasRuleSets.Cancun.CalculateFloorGasLimit(data, isContractCreation: false));
        }

        // -----------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------

        private static byte[] BuildMixedData(int length)
        {
            // Alternate zero and non-zero bytes so both data-byte cost
            // branches are exercised and the EIP-7623 token count is
            // non-trivial. Even indices are zero, odd indices are 0xAB.
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
