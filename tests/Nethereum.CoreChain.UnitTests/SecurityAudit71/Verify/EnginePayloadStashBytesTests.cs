using System;
using System.Collections.Generic;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.Verify
{
    public class EnginePayloadStashBytesTests
    {
        private const long BudgetBytes = EnginePayloadStashOptions.DefaultMaxBytes;

        private readonly ITestOutputHelper _out;

        public EnginePayloadStashBytesTests(ITestOutputHelper output) { _out = output; }

        [Theory]
        [InlineData(131072)]
        [InlineData(262144)]
        public void Given_PayloadsOfDistinctHashesStashedPastCapacity_When_TheRetainedBytesAreMeasured_Then_TheyStayUnderTheByteBudget(int transactionDataBytes)
        {
            var stash = new EnginePayloadStash();
            var before = GC.GetTotalMemory(true);
            for (var i = 0; i < 2048; i++)
            {
                var hash = new byte[32];
                hash[0] = (byte)(i >> 8);
                hash[1] = (byte)i;
                var tx = new Transaction1559(
                    (EvmUInt256)1, EvmUInt256.Zero, EvmUInt256.Zero, EvmUInt256.Zero, (EvmUInt256)30_000_000,
                    "0x0000000000000000000000000000000000000001", EvmUInt256.Zero, "0x" + new string((char)(97 + i % 6), transactionDataBytes * 2), new List<AccessListItem>());
                var header = new BlockHeader { BlockNumber = i + 1, ParentHash = new byte[32] };
                stash.Put(new BlockBundle(
                    header, new List<ISignedTransaction> { tx }, new List<BlockHeader>(), new List<Withdrawal>(), hash));
            }
            var retained = GC.GetTotalMemory(true) - before;
            _out.WriteLine($"txDataBytes={transactionDataBytes} count={stash.Count} retainedMiB={retained / 1048576}");
            GC.KeepAlive(stash);
            Assert.True(stash.RetainedBytes <= BudgetBytes && retained <= BudgetBytes + BudgetBytes / 4, $"retained {retained / 1048576} MiB against a budget of {BudgetBytes / 1048576} MiB");
        }
    }
}
