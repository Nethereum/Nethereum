using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain.Models;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Models
{
    public class BlockWideLogIndexTests
    {
        private static FilteredLog Log(int txIndex, int positionWithinTx) => new FilteredLog
        {
            TransactionIndex = txIndex,
            LogIndex = positionWithinTx
        };

        [Fact]
        public void Given_TxIndexAcross256_InScrambledOrder_When_Assign_Then_NumberedInTxOrder()
        {
            var logs = new List<FilteredLog> { Log(256, 0), Log(1, 0), Log(0, 0) };

            BlockWideLogIndex.Assign(logs);

            Assert.Equal(new[] { 0, 1, 256 }, logs.Select(l => l.TransactionIndex).ToArray());
            Assert.Equal(new[] { 0, 1, 2 }, logs.Select(l => l.LogIndex).ToArray());
        }

        [Fact]
        public void Given_SingleTxWith300Logs_InScrambledOrder_When_Assign_Then_NumberedByPositionWithinTx()
        {
            var logs = Enumerable.Range(0, 300).Select(p => Log(0, p)).ToList();
            logs.Reverse();

            BlockWideLogIndex.Assign(logs);

            Assert.Equal(Enumerable.Range(0, 300).ToArray(), logs.Select(l => l.LogIndex).ToArray());
        }

        [Fact]
        public void Given_MultiTxMultiLog_When_Assign_Then_IndexIsRunningCountAcrossTransactions()
        {
            var logs = new List<FilteredLog>
            {
                Log(2, 2), Log(0, 1), Log(1, 0), Log(2, 0), Log(0, 0), Log(2, 1)
            };

            BlockWideLogIndex.Assign(logs);

            Assert.Equal(new[] { (0, 0), (0, 1), (1, 2), (2, 3), (2, 4), (2, 5) },
                logs.Select(l => (l.TransactionIndex, l.LogIndex)).ToArray());
        }

        [Fact]
        public void Given_SingleLog_When_Assign_Then_IndexIsZero()
        {
            var logs = new List<FilteredLog> { Log(7, 3) };

            BlockWideLogIndex.Assign(logs);

            Assert.Equal(0, logs[0].LogIndex);
        }

        [Fact]
        public void Given_EmptyOrNull_When_Assign_Then_NoThrow()
        {
            BlockWideLogIndex.Assign(new List<FilteredLog>());
            BlockWideLogIndex.Assign(null);
        }
    }
}
