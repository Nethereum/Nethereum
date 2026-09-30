using System.Collections.Generic;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class ReceiptExtensionsStartingLogIndexTests
    {
        private static ReceiptInfo ReceiptWithLogCount(int logCount)
        {
            var logs = new List<Log>();
            for (int i = 0; i < logCount; i++) logs.Add(new Log());

            return new ReceiptInfo
            {
                Receipt = new Receipt { Logs = logs }
            };
        }

        [Fact]
        public void Given_ThirdTxInBlock_When_ComputeStartingLogIndex_Then_SumsLogsOfPrecedingTwoTxs()
        {
            var receiptsInBlockOrder = new List<ReceiptInfo>
            {
                ReceiptWithLogCount(2),
                ReceiptWithLogCount(2),
                ReceiptWithLogCount(1)
            };

            var startingLogIndex = ReceiptExtensions.ComputeStartingLogIndex(receiptsInBlockOrder, transactionIndex: 2);

            Assert.Equal(4, startingLogIndex);
        }

        [Fact]
        public void Given_FirstTxInBlock_When_ComputeStartingLogIndex_Then_IsZero()
        {
            var receiptsInBlockOrder = new List<ReceiptInfo>
            {
                ReceiptWithLogCount(3)
            };

            var startingLogIndex = ReceiptExtensions.ComputeStartingLogIndex(receiptsInBlockOrder, transactionIndex: 0);

            Assert.Equal(0, startingLogIndex);
        }

        [Fact]
        public void Given_MissingReceiptAmongPrecedingTxs_When_ComputeStartingLogIndex_Then_TreatsItAsZeroLogs()
        {
            var receiptsInBlockOrder = new List<ReceiptInfo>
            {
                ReceiptWithLogCount(2),
                null,
                ReceiptWithLogCount(3)
            };

            var startingLogIndex = ReceiptExtensions.ComputeStartingLogIndex(receiptsInBlockOrder, transactionIndex: 2);

            Assert.Equal(2, startingLogIndex);
        }

        [Fact]
        public void Given_TransactionReceiptHandlerShape_And_BlockReceiptsHandlerShape_When_BothCallHelper_Then_AgreeOnStartingLogIndex()
        {
            var receiptsInBlockOrder = new List<ReceiptInfo>
            {
                ReceiptWithLogCount(2),
                ReceiptWithLogCount(2),
                ReceiptWithLogCount(1)
            };

            var fromBlockReceiptsHandler = ReceiptExtensions.ComputeStartingLogIndex(receiptsInBlockOrder, transactionIndex: 2);

            var precedingOnly = receiptsInBlockOrder.GetRange(0, 2);
            var fromTransactionReceiptHandler = ReceiptExtensions.ComputeStartingLogIndex(precedingOnly, transactionIndex: precedingOnly.Count);

            Assert.Equal(4, fromBlockReceiptsHandler);
            Assert.Equal(fromBlockReceiptsHandler, fromTransactionReceiptHandler);
        }
    }
}
