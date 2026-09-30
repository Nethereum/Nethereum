using System.Collections.Generic;
using Nethereum.EVM.Gas.Intrinsic;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    /// <summary>
    /// AMS-4844-04. EIP-4844 §Gas accounting:
    /// "<c>def get_total_blob_gas(tx: Transaction) -&gt; int: return GAS_PER_BLOB * len(tx.blob_versioned_hashes)</c>"
    /// — an unbounded scalar, which EELS types as <c>U64</c>
    /// (<c>GasCosts.PER_BLOB * U64(len(tx.blob_versioned_hashes))</c>).
    ///
    /// <para>A 32-bit product wraps at a blob count of 2^14 and, once widened
    /// unsigned, reports a total FAR LARGER than the true one. The count is
    /// capped well below that on the mainnet consensus path, so these tests
    /// pin the arithmetic itself, across every fork rule and the shared
    /// calculator the rules delegate to.</para>
    /// </summary>
    public class BlobGasFullWidthTests
    {
        private const int GasPerBlob = 131072;
        private const int BlobCountThatWrapsThirtyTwoBits = 16384;
        private const ulong TotalBlobGasAtWrappingCount = 2147483648UL;
        private const ulong WrappedTotalBlobGas = 18446744071562067968UL;

        private const int OrdinaryBlobCount = 6;
        private const ulong OrdinaryTotalBlobGas = 786432UL;

        private static readonly EvmUInt256 BlobBaseFee = new EvmUInt256(7);

        public static IEnumerable<object[]> EveryBlobGasRule()
        {
            yield return new object[] { Eip4844BlobGasRule.Instance };
            yield return new object[] { Eip7691BlobGasRule.Instance };
            yield return new object[] { Eip7892BlobGasRule.Instance };
            yield return new object[] { Eip7892Bpo1BlobGasRule.Instance };
            yield return new object[] { Eip7892Bpo2BlobGasRule.Instance };
            yield return new object[] { AmsterdamBlobGasRule.Instance };
        }

        [Theory]
        [MemberData(nameof(EveryBlobGasRule))]
        public void Given_ABlobCountAboveTwoToTheFourteen_When_BlobGasIsComputed_Then_ItDoesNotOverflow(IBlobGasRule rule)
        {
            var cost = rule.CalculateBlobGasCost(BlobCountThatWrapsThirtyTwoBits, BlobBaseFee);

            Assert.Equal(new EvmUInt256(TotalBlobGasAtWrappingCount) * BlobBaseFee, cost);
            Assert.NotEqual(new EvmUInt256(WrappedTotalBlobGas) * BlobBaseFee, cost);
        }

        [Theory]
        [MemberData(nameof(EveryBlobGasRule))]
        public void Given_AnOrdinaryBlobCount_When_Computed_Then_ItIsUnchanged(IBlobGasRule rule)
        {
            var cost = rule.CalculateBlobGasCost(OrdinaryBlobCount, BlobBaseFee);

            Assert.Equal(new EvmUInt256(OrdinaryTotalBlobGas) * BlobBaseFee, cost);
        }

        [Fact]
        public void Given_ABlobCountAboveTwoToTheFourteen_When_TotalBlobGasIsComputed_Then_ItDoesNotOverflow()
        {
            Assert.Equal(TotalBlobGasAtWrappingCount, BlobGasCalculator.CalculateTotalBlobGas(BlobCountThatWrapsThirtyTwoBits));
        }

        [Fact]
        public void Given_AnOrdinaryBlobCount_When_TotalBlobGasIsComputed_Then_ItIsUnchanged()
        {
            Assert.Equal(OrdinaryTotalBlobGas, BlobGasCalculator.CalculateTotalBlobGas(OrdinaryBlobCount));
        }

        [Fact]
        public void Given_EveryBlobCountUpToTheCap_When_TotalBlobGasIsComputed_Then_ItIsGasPerBlobTimesTheCount()
        {
            for (var blobCount = 0; blobCount <= 21; blobCount++)
                Assert.Equal((ulong)blobCount * GasPerBlob, BlobGasCalculator.CalculateTotalBlobGas(blobCount));
        }
    }
}
