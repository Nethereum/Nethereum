using System.Linq;
using System.Reflection;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Gas.Intrinsic;
using Nethereum.EVM.Hardforks;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Hardforks
{
    public class OsakaBpo1ForkTests
    {
        private const int OsakaFraction = 5_007_716;
        private const int OsakaBpo1Fraction = 8_346_193;

        [Fact]
        public void Osaka_blob_fraction_is_unchanged_from_Prague()
        {
            Assert.Equal(OsakaFraction, Eip7892BlobGasRule.DEFAULT_BASE_FEE_UPDATE_FRACTION);
        }

        [Fact]
        public void OsakaBpo1_blob_fraction_is_the_first_post_Osaka_bump()
        {
            var excess = new EvmUInt256(50_000_000UL);
            var expected = ReferenceFakeExponential(1, 50_000_000L, OsakaBpo1Fraction);

            var actual = IntrinsicGasRuleSets.OsakaBpo1.Blob.CalculateBlobBaseFee(excess);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Osaka_blob_fraction_at_a_fixed_excess_matches_the_reference_formula()
        {
            var excess = new EvmUInt256(50_000_000UL);
            var expected = ReferenceFakeExponential(1, 50_000_000L, OsakaFraction);

            var actual = IntrinsicGasRuleSets.Osaka.Blob.CalculateBlobBaseFee(excess);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void OsakaBpo1_installs_its_own_blob_rule_not_Osakas()
        {
            Assert.Same(Eip7892Bpo1BlobGasRule.Instance, IntrinsicGasRuleSets.OsakaBpo1.Blob);
            Assert.NotSame(IntrinsicGasRuleSets.Osaka.Blob, IntrinsicGasRuleSets.OsakaBpo1.Blob);
        }

        [Fact]
        public void OsakaBpo1_changes_the_blob_base_fee_and_nothing_else_in_the_intrinsic_bundle()
        {
            var osaka = IntrinsicGasRuleSets.Osaka;
            var bpo1 = IntrinsicGasRuleSets.OsakaBpo1;

            Assert.Equal(osaka.TxBase, bpo1.TxBase);
            Assert.Equal(osaka.TxCreate, bpo1.TxCreate);
            Assert.Equal(osaka.TxDataZero, bpo1.TxDataZero);
            Assert.Equal(osaka.TxDataNonZero, bpo1.TxDataNonZero);
            Assert.Same(osaka.InitCode, bpo1.InitCode);
            Assert.Same(osaka.AccessList, bpo1.AccessList);
            Assert.Same(osaka.Floor, bpo1.Floor);
            Assert.Same(osaka.Recipient, bpo1.Recipient);
        }

        [Theory]
        [InlineData(0UL)]
        [InlineData(50_000_000UL)]
        [InlineData(120_000_000UL)]
        public void OsakaBpo1_blob_base_fee_differs_from_Osaka_above_zero_excess(ulong excessBlobGas)
        {
            var excess = new EvmUInt256(excessBlobGas);

            var osakaFee = IntrinsicGasRuleSets.Osaka.Blob.CalculateBlobBaseFee(excess);
            var bpo1Fee = IntrinsicGasRuleSets.OsakaBpo1.Blob.CalculateBlobBaseFee(excess);

            if (excessBlobGas == 0)
            {
                Assert.Equal(EvmUInt256.One, osakaFee);
                Assert.Equal(EvmUInt256.One, bpo1Fee);
                return;
            }

            Assert.True(bpo1Fee < osakaFee,
                $"expected BPO1 fee below Osaka's at excess {excessBlobGas}; got {bpo1Fee} vs {osakaFee}");
        }

        [Fact]
        public void OsakaBpo1_spec_differs_from_Osaka_only_in_its_intrinsic_gas_bundle_and_blob_count()
        {
            var osakaPrecompiles = OsakaSpec.Instance.Precompiles
                .Select(p => (p.Address, p.Kind)).ToList();
            var bpo1Precompiles = OsakaBpo1Spec.Instance.Precompiles
                .Select(p => (p.Address, p.Kind)).ToList();
            Assert.Equal(osakaPrecompiles, bpo1Precompiles);

            var differing = typeof(HardforkSpec)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .Where(p => p.Name != nameof(HardforkSpec.Precompiles))
                .Where(p => !Equals(p.GetValue(OsakaSpec.Instance), p.GetValue(OsakaBpo1Spec.Instance)))
                .Select(p => p.Name)
                .OrderBy(n => n)
                .ToList();

            Assert.True(
                typeof(HardforkSpec).GetProperties(BindingFlags.Public | BindingFlags.Instance).Length > 0,
                "no HardforkSpec properties discovered - the reflection filter no longer matches");

            Assert.Equal(new[] { "IntrinsicGas", "MaxBlobsPerBlock", "Name" }, differing);
        }

        [Fact]
        public void OsakaBpo1_max_blobs_per_block_is_15_not_Osakas_9()
        {
            Assert.Equal(15, OsakaBpo1Spec.Instance.MaxBlobsPerBlock);
            Assert.Equal(9, OsakaSpec.Instance.MaxBlobsPerBlock);
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
