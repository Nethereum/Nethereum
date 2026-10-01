using System.Linq;
using System.Reflection;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Gas.Intrinsic;
using Nethereum.EVM.Hardforks;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Hardforks
{
    public class OsakaBpo2ForkTests
    {
        private const int OsakaBpo1Fraction = 8_346_193;
        private const int OsakaBpo2Fraction = 11_684_671;

        [Fact]
        public void OsakaBpo1_blob_fraction_at_a_fixed_excess_matches_the_reference_formula()
        {
            var excess = new EvmUInt256(50_000_000UL);
            var expected = ReferenceFakeExponential(1, 50_000_000L, OsakaBpo1Fraction);

            var actual = IntrinsicGasRuleSets.OsakaBpo1.Blob.CalculateBlobBaseFee(excess);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void OsakaBpo2_blob_fraction_is_the_second_post_Osaka_bump()
        {
            var excess = new EvmUInt256(50_000_000UL);
            var expected = ReferenceFakeExponential(1, 50_000_000L, OsakaBpo2Fraction);

            var actual = IntrinsicGasRuleSets.OsakaBpo2.Blob.CalculateBlobBaseFee(excess);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void OsakaBpo2_installs_its_own_blob_rule_not_Bpo1s()
        {
            Assert.Same(Eip7892Bpo2BlobGasRule.Instance, IntrinsicGasRuleSets.OsakaBpo2.Blob);
            Assert.NotSame(IntrinsicGasRuleSets.OsakaBpo1.Blob, IntrinsicGasRuleSets.OsakaBpo2.Blob);
        }

        [Fact]
        public void OsakaBpo2_changes_the_blob_base_fee_and_nothing_else_in_the_intrinsic_bundle()
        {
            var bpo1 = IntrinsicGasRuleSets.OsakaBpo1;
            var bpo2 = IntrinsicGasRuleSets.OsakaBpo2;

            Assert.Equal(bpo1.TxBase, bpo2.TxBase);
            Assert.Equal(bpo1.TxCreate, bpo2.TxCreate);
            Assert.Equal(bpo1.TxDataZero, bpo2.TxDataZero);
            Assert.Equal(bpo1.TxDataNonZero, bpo2.TxDataNonZero);
            Assert.Same(bpo1.InitCode, bpo2.InitCode);
            Assert.Same(bpo1.AccessList, bpo2.AccessList);
            Assert.Same(bpo1.Floor, bpo2.Floor);
            Assert.Same(bpo1.Recipient, bpo2.Recipient);
        }

        [Theory]
        [InlineData(0UL)]
        [InlineData(50_000_000UL)]
        [InlineData(120_000_000UL)]
        public void OsakaBpo2_blob_base_fee_differs_from_Bpo1_above_zero_excess(ulong excessBlobGas)
        {
            var excess = new EvmUInt256(excessBlobGas);

            var bpo1Fee = IntrinsicGasRuleSets.OsakaBpo1.Blob.CalculateBlobBaseFee(excess);
            var bpo2Fee = IntrinsicGasRuleSets.OsakaBpo2.Blob.CalculateBlobBaseFee(excess);

            if (excessBlobGas == 0)
            {
                Assert.Equal(EvmUInt256.One, bpo1Fee);
                Assert.Equal(EvmUInt256.One, bpo2Fee);
                return;
            }

            Assert.True(bpo2Fee < bpo1Fee,
                $"expected BPO2 fee below BPO1's at excess {excessBlobGas}; got {bpo2Fee} vs {bpo1Fee}");
        }

        [Fact]
        public void OsakaBpo2_spec_differs_from_Bpo1_only_in_its_intrinsic_gas_bundle_and_blob_count()
        {
            var bpo1Precompiles = OsakaBpo1Spec.Instance.Precompiles
                .Select(p => (p.Address, p.Kind)).ToList();
            var bpo2Precompiles = OsakaBpo2Spec.Instance.Precompiles
                .Select(p => (p.Address, p.Kind)).ToList();
            Assert.Equal(bpo1Precompiles, bpo2Precompiles);

            var differing = typeof(HardforkSpec)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .Where(p => p.Name != nameof(HardforkSpec.Precompiles))
                .Where(p => !Equals(p.GetValue(OsakaBpo1Spec.Instance), p.GetValue(OsakaBpo2Spec.Instance)))
                .Select(p => p.Name)
                .OrderBy(n => n)
                .ToList();

            Assert.True(
                typeof(HardforkSpec).GetProperties(BindingFlags.Public | BindingFlags.Instance).Length > 0,
                "no HardforkSpec properties discovered - the reflection filter no longer matches");

            Assert.Equal(new[] { "IntrinsicGas", "MaxBlobsPerBlock", "Name", "TargetBlobsPerBlock" }, differing);
        }

        [Fact]
        public void OsakaBpo2_max_blobs_per_block_is_21_not_Bpo1s_15()
        {
            Assert.Equal(21, OsakaBpo2Spec.Instance.MaxBlobsPerBlock);
            Assert.Equal(15, OsakaBpo1Spec.Instance.MaxBlobsPerBlock);
        }

        [Fact]
        public void Amsterdam_blob_schedule_does_not_leak_to_or_from_BPO2()
        {
            Assert.NotSame(Eip7892Bpo2BlobGasRule.Instance, IntrinsicGasRuleSets.Amsterdam.Blob);
            Assert.IsType<AmsterdamBlobGasRule>(IntrinsicGasRuleSets.Amsterdam.Blob);

            Assert.Equal(11_684_671, AmsterdamBlobGasRule.DEFAULT_BASE_FEE_UPDATE_FRACTION);
            Assert.Equal(OsakaBpo2Fraction, AmsterdamBlobGasRule.DEFAULT_BASE_FEE_UPDATE_FRACTION);
            Assert.Equal(21, AmsterdamSpec.Instance.MaxBlobsPerBlock);
            Assert.Equal(AmsterdamSpec.Instance.MaxBlobsPerBlock, OsakaBpo2Spec.Instance.MaxBlobsPerBlock);
        }

        private static EvmUInt256 ReferenceFakeExponential(long factor, long numerator, long denominator)
        {
            var i = 1L;
            var output = System.Numerics.BigInteger.Zero;
            var numeratorAccum = (System.Numerics.BigInteger)factor * denominator;
            while (numeratorAccum != 0)
            {
                output += numeratorAccum;
                numeratorAccum = (numeratorAccum * numerator) / (denominator * i);
                i += 1;
            }
            return EvmUInt256BigIntegerExtensions.FromBigInteger(output / denominator);
        }
    }
}
