using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Sync;
using Nethereum.MainnetChain.Replay;
using Nethereum.Model;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class ReplayRecorderTimingTests
    {
        [Fact]
        public async Task Given_ABlockWhoseStagesTakeUnderAMillisecond_When_TheTimingLineIsLogged_Then_TheRecordKeepsTheFractionalMilliseconds()
        {
            using var recorder = new ReplayRecorder(
                new AlwaysAcceptConsensusBlockGate(), new EveryBlockFlushCadence(), 0, null, (_, _) => { });
            var logger = recorder.CreateLogger("Nethereum.CoreChain.BlockExecutor");
            var header = new BlockHeader { BlockNumber = new BigInteger(1) };

            await recorder.IsBlockCanonicalAsync(header, new byte[32], CancellationToken.None);
            logger.LogInformation(
                "block.exec.timing block={BlockNumber} txs={TxCount} gas={Gas} exec_ms={ExecMs:F3} root_ms={RootMs:F3} persist_ms={PersistMs:F3} gc1={Gc1} gc2={Gc2} alloc_mb={AllocMb} acct_reads={AcctReads} slot_reads={SlotReads}",
                1UL, 2, 21000L, 0.4, 0.25, 0.125, 0, 0, 1L, 3L, 4L);
            await recorder.OnBlockImportedAsync(header, new byte[32], CancellationToken.None);

            var record = Assert.Single(recorder.Blocks);
            Assert.Equal(0.4, record.ExecMs, 3);
            Assert.Equal(0.25, record.RootMs, 3);
            Assert.Equal(0.125, record.PersistMs, 3);
        }
    }
}
