using System.Collections.Generic;
using Nethereum.Contracts;
using Nethereum.Contracts.Comparers;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Filters;
using Xunit;

namespace Nethereum.Web3.Tests
{
    public class EventLogSortingTests
    {
        public class MyEvent
        {

        }

        [Fact]
        public void ShouldSortBasedOnBlockNumber()
        {
            //given
            var filterLog = new FilterLog()
            {
                BlockNumber = new HexBigInteger(1),
                TransactionIndex = new HexBigInteger(1)
            };
            var eventLog = new EventLog<MyEvent>(null, filterLog);
            var filterLog2 = new FilterLog()
            {
                BlockNumber = new HexBigInteger(2),
                TransactionIndex = new HexBigInteger(1)
            };
            var eventLog2 = new EventLog<MyEvent>(null, filterLog2);

            var filterLog3 = new FilterLog()
            {
                BlockNumber = new HexBigInteger(3),
                TransactionIndex = new HexBigInteger(1)
            };
            var eventLog3 = new EventLog<MyEvent>(null, filterLog3);

            var list = new List<object>(new[] {eventLog3, eventLog, eventLog2});
            list.Sort(new EventLogBlockNumberTransactionIndexComparer());
            Assert.Same(eventLog, list[0]);
            Assert.Same(eventLog2, list[1]);
            Assert.Same(eventLog3, list[2]);
        }

        [Fact]
        public void ShouldSortBasedOnBlockNumberAndTransaction()
        {
            //given
            var filterLog = new FilterLog()
            {
                BlockNumber = new HexBigInteger(1),
                TransactionIndex = new HexBigInteger(1)
            };
            var eventLog = new EventLog<MyEvent>(null, filterLog);
            var filterLog2 = new FilterLog()
            {
                BlockNumber = new HexBigInteger(2),
                TransactionIndex = new HexBigInteger(1)
            };
            var eventLog2 = new EventLog<MyEvent>(null, filterLog2);

            var filterLog3 = new FilterLog()
            {
                BlockNumber = new HexBigInteger(2),
                TransactionIndex = new HexBigInteger(2)
            };
            var eventLog3 = new EventLog<MyEvent>(null, filterLog3);

            var list = new List<object>(new[] {eventLog3, eventLog, eventLog2});
            list.Sort(new EventLogBlockNumberTransactionIndexComparer());
            Assert.Same(eventLog, list[0]);
            Assert.Same(eventLog2, list[1]);
            Assert.Same(eventLog3, list[2]);
        }
    }
}