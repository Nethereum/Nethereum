using System.Collections.Generic;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.Eth.DTOs.Comparers;
using Nethereum.Contracts.CQS;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Contracts.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.Contracts.UnitTests
{

    public class EncodingABIExtensionTests
    {

        public class TestParamsInput:ContractMessageBase
        {
            [Parameter("string", 1)] public string First { get; set; }
            [Parameter("int8", 2)] public int Second { get; set; }
            [Parameter("address", 3)] public string Third { get; set; }
        }

        [Fact]
        public virtual void ShouldEncodeSha3PackedParams()
        {
            var abiEncode = new ABIEncode();
            var result = new TestParamsInput()
                { First = "Hello!%", Second = -23, Third = "0x85F43D8a49eeB85d32Cf465507DD71d507100C1d" }.GetSha3ParamsEncodedPacked();
            Assert.Equal("0xa13b31627c1ed7aaded5aecec71baf02fe123797fffd45e662eac8e06fbe4955", result.ToHex(true));
        }


        [Fact]
        public virtual void ShouldEncodeParams()
        {
            var paramsEncoded =
                "0000000000000000000000000000000000000000000000000000000000000060000000000000000000000000000000000000000000000000000000000000004500000000000000000000000000000000000000000000000000000000000000a0000000000000000000000000000000000000000000000000000000000000000568656c6c6f0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000005776f726c64000000000000000000000000000000000000000000000000000000";
            var abiEncode = new ABIEncode();
            var test = new TestParamsInputEncoded() { First = "hello", Second = 69, Third = "world" };
            var result = test.GetParamsEncoded();
            Assert.Equal("0x" + paramsEncoded, result.ToHex(true));
        }

        public class TestParamsInputEncoded:ContractMessageBase
        {
            [Parameter("string", 1)]
            public string First { get; set; }
            [Parameter("int256", 2)]
            public int Second { get; set; }
            [Parameter("string", 3)]
            public string Third { get; set; }
        }
    }


    public class EventLogSortingTests
    {
        public class MyEvent
        {
        }

        [Fact]
        public void ShouldSortBasedOnBlockNumber()
        {
            //given
            var filterLog = new FilterLog
            {
                BlockNumber = new HexBigInteger(1),
                TransactionIndex = new HexBigInteger(1)
            };
            var eventLog = new EventLog<MyEvent>(null, filterLog);
            var filterLog2 = new FilterLog
            {
                BlockNumber = new HexBigInteger(2),
                TransactionIndex = new HexBigInteger(1)
            };
            var eventLog2 = new EventLog<MyEvent>(null, filterLog2);

            var filterLog3 = new FilterLog
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
            var filterLog = new FilterLog
            {
                BlockNumber = new HexBigInteger(1),
                TransactionIndex = new HexBigInteger(1)
            };
            var eventLog = new EventLog<MyEvent>(null, filterLog);
            var filterLog2 = new FilterLog
            {
                BlockNumber = new HexBigInteger(2),
                TransactionIndex = new HexBigInteger(1)
            };
            var eventLog2 = new EventLog<MyEvent>(null, filterLog2);

            var filterLog3 = new FilterLog
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