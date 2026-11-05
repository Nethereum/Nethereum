using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Model;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests.SecurityAudit71.S2
{
    public class S2ReceiptAssemblyTests
    {
        private const int MaxMainnetBlockTransactions = 60_000_000 / 21_000;

        [Fact]
        public async Task S2_04_Given_APeerKeepsAnsweringTheFirstBlockIncompleteWithMoreReceipts_When_ThePartialReceiptsAreAssembled_Then_AssemblyAbortsOnceTheBlockHoldsMoreReceiptsThanAnyBlockCan()
        {
            var receipt = new Receipt();
            var chunk = Enumerable.Repeat(receipt, 1000).ToList();
            var served = 0;

            var outcome = await Record.ExceptionAsync(() => SyncPeerSession.AssembleReceipts70Async(
                hashCount: 1,
                fetchFromPos: (pos, firstIndex) =>
                {
                    served++;
                    return Task.FromResult(((IReadOnlyList<List<Receipt>>)new List<List<Receipt>> { chunk }, true));
                },
                maxRounds: 4096));

            var carried = (long)served * chunk.Count;
            Assert.True(
                outcome != null || carried <= 2 * MaxMainnetBlockTransactions,
                $"{served} rounds were served and {carried} receipts were accumulated for one block; a mainnet block holds at most {MaxMainnetBlockTransactions}");
        }
    }
}
