using System;
using System.Collections.Generic;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class ReceiptGasAccountingTests
    {
        private static readonly EvmUInt256 BlockNumber = new EvmUInt256(1234UL);

        private static Receipt WithCumulativeGas(ulong cumulativeGasUsed) => new Receipt
        {
            PostStateOrStatus = new byte[] { 1 },
            CumulativeGasUsed = new EvmUInt256(cumulativeGasUsed),
            Bloom = new byte[256],
            Logs = new List<Log>(),
        };

        private static List<Receipt> Cumulatives(params ulong[] figures)
        {
            var receipts = new List<Receipt>(figures.Length);
            foreach (var figure in figures) receipts.Add(WithCumulativeGas(figure));
            return receipts;
        }

        [Fact]
        public void Given_RisingCumulativeGas_When_Differenced_Then_EachRowGetsItsOwnSpend()
        {
            var gasUsed = ReceiptGasAccounting.PerTransactionGasUsed(
                Cumulatives(21_000, 71_000, 92_000), BlockNumber);

            Assert.Equal(
                new[] { new EvmUInt256(21_000UL), new EvmUInt256(50_000UL), new EvmUInt256(21_000UL) },
                gasUsed);
        }

        [Fact]
        public void Given_TwoReceiptsReportingTheSameCumulativeGas_When_Differenced_Then_TheSecondSpentNothing()
        {
            var gasUsed = ReceiptGasAccounting.PerTransactionGasUsed(Cumulatives(21_000, 21_000), BlockNumber);

            Assert.Equal(new EvmUInt256(21_000UL), gasUsed[0]);
            Assert.Equal(EvmUInt256.Zero, gasUsed[1]);
        }

        [Fact]
        public void Given_CumulativeGasThatFalls_When_Differenced_Then_ItThrowsNamingTheBlockAndRow()
        {
            var ex = Assert.Throws<NonMonotonicCumulativeGasException>(
                () => ReceiptGasAccounting.PerTransactionGasUsed(Cumulatives(21_000, 10_000), BlockNumber));

            Assert.Equal(BlockNumber, ex.BlockNumber);
            Assert.Equal(1, ex.ReceiptIndex);
            Assert.Equal(new EvmUInt256(21_000UL), ex.PreviousCumulativeGasUsed);
            Assert.Equal(new EvmUInt256(10_000UL), ex.CumulativeGasUsed);
        }

        [Fact]
        public void Given_AFallLateInTheBlock_When_Differenced_Then_TheEarlierRowsAreNotReturnedEither()
        {
            var ex = Assert.Throws<NonMonotonicCumulativeGasException>(
                () => ReceiptGasAccounting.PerTransactionGasUsed(
                    Cumulatives(21_000, 42_000, 63_000, 50_000), BlockNumber));

            Assert.Equal(3, ex.ReceiptIndex);
        }

        [Fact]
        public void Given_NoReceipts_When_Differenced_Then_ThereIsNothingToAccountFor()
        {
            Assert.Empty(ReceiptGasAccounting.PerTransactionGasUsed(new List<Receipt>(), BlockNumber));
        }

        [Fact]
        public void Given_NoReceiptListAtAll_When_Differenced_Then_ItIsRefusedRatherThanReadAsAnEmptyBlock()
        {
            Assert.Throws<ArgumentNullException>(
                () => ReceiptGasAccounting.PerTransactionGasUsed(null, BlockNumber));
        }

    }
}
