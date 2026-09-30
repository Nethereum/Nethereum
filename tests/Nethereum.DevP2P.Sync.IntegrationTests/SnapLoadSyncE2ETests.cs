using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    public class SnapLoadSyncE2ETests
    {
        private readonly ITestOutputHelper _output;

        public SnapLoadSyncE2ETests(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task Given_TheLoadGenerator_When_ItRuns_Then_TheChainCarriesContractCodeAndStorage()
        {
            var node = DevChainNode.Create();
            try
            {
                await node.StartAsync();
                var producer = await DevChainProducer.AttachAsync(node);

                var load = await DevChainLoad.GenerateAsync(node, producer, blocks: 12, contracts: 2);

                _output.WriteLine(
                    $"height={load.Height} txs={load.Transactions} contracts={load.ContractAddresses.Count}");

                Assert.True(load.Height >= 12, $"load produced only {load.Height} blocks");
                Assert.True(load.Transactions > 0, "load produced no transactions");

                var withCode = 0;
                var withStorage = 0;
                foreach (var address in load.ContractAddresses)
                {
                    var account = await node.Bundle.State.GetAccountAsync(address.ToLowerInvariant());
                    if (account?.CodeHash == null) continue;

                    var code = await node.Bundle.State.GetCodeAsync(account.CodeHash);
                    if (code != null && code.Length > 0) withCode++;

                    var slot = await node.Bundle.State.GetStorageAsync(address.ToLowerInvariant(), 0);
                    if (slot != null && slot.Any(b => b != 0)) withStorage++;

                    _output.WriteLine($"  {address}: code={code?.Length ?? 0} bytes slot0=0x{slot?.ToHex()}");
                }

                Assert.True(withCode > 0,
                    "no deployed contract carries bytecode — snap's bytecode path would never be exercised");
                Assert.True(withStorage > 0,
                    "no deployed contract carries storage — snap's storage path would never be exercised");
            }
            finally
            {
                await node.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_AChainUnderLoad_When_AColdNodeSnapSyncs_Then_ItCompletesEveryPhaseAndItsStateMatches()
        {
            var producerNode = DevChainNode.CreateWithRocksDb();
            var joiner = DevChainNode.CreateWithRocksDb();
            try
            {
                await producerNode.StartAsync();
                var producer = await DevChainProducer.AttachAsync(producerNode);

                var load = await DevChainLoad.GenerateAsync(producerNode, producer, blocks: 80, contracts: 4);
                _output.WriteLine($"load: height={load.Height} txs={load.Transactions} contracts={load.ContractAddresses.Count}");

                await joiner.StartAsync();
                await joiner.ConnectToAsync(producerNode);

                var follower = await DevChainFollower.AttachAsync(joiner);
                try
                {
                    using var deadline = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(4));
                    var result = await follower.SnapBootstrapAsync(deadline.Token, new XunitLogger(_output));

                    Assert.True(result.Ran, $"snap bootstrap did not run: {result.SkipReason}");
                    _output.WriteLine(
                        $"snap pivot={result.PivotBlockNumber} accounts={result.AccountCount} " +
                        $"slots={result.SlotCount} bytecode={result.BytecodeCount}");

                    Assert.True(result.AccountCount > 0, "no accounts synced");
                    Assert.True(result.SlotCount > 0, "no storage slots synced — the storage phase did no work");
                    Assert.True(result.BytecodeCount > 0, "no bytecode synced — the bytecode phase did no work");

                    Assert.Null(joiner.Bundle.Metadata.GetSnapSyncState());

                    var pivot = (long)result.PivotBlockNumber;

                    var expectedRoot = (await producerNode.Bundle.Blocks.GetByNumberAsync(pivot))?.StateRoot;
                    Assert.True(ByteUtil.AreEqual(result.PivotStateRoot, expectedRoot),
                        "synced pivot state root does not match the producer's");

                    foreach (var address in load.ContractAddresses)
                    {
                        var key = address.ToLowerInvariant();
                        var mine = await joiner.Bundle.State.GetAccountAsync(key);
                        var theirs = await producerNode.Bundle.State.GetAccountAsync(key);
                        Assert.NotNull(mine);

                        Assert.True(ByteUtil.AreEqual(mine.CodeHash, theirs.CodeHash),
                            $"code hash differs for {address}");

                        var myCode = await joiner.Bundle.State.GetCodeAsync(mine.CodeHash);
                        var theirCode = await producerNode.Bundle.State.GetCodeAsync(theirs.CodeHash);
                        Assert.True(myCode != null && myCode.Length > 0, $"no code synced for {address}");
                        Assert.True(ByteUtil.AreEqual(myCode, theirCode), $"code bytes differ for {address}");

                        for (var slot = 0; slot < 3; slot++)
                        {
                            var mySlot = await joiner.Bundle.State.GetStorageAsync(key, slot);
                            var theirSlot = await producerNode.Bundle.State.GetStorageAsync(key, slot);
                            Assert.True(ByteUtil.AreEqual(mySlot, theirSlot),
                                $"storage slot {slot} differs for {address}");
                        }
                    }

                    _output.WriteLine($"snap synced to pivot {pivot}: code and storage match for {load.ContractAddresses.Count} contracts");
                }
                finally
                {
                    await follower.DisposeAsync();
                }
            }
            finally
            {
                await joiner.DisposeAsync();
                await producerNode.DisposeAsync();
            }
        }

        private sealed class XunitLogger : Microsoft.Extensions.Logging.ILogger
        {
            private readonly ITestOutputHelper _out;

            public XunitLogger(ITestOutputHelper output) => _out = output;

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

            public void Log<TState>(
                Microsoft.Extensions.Logging.LogLevel logLevel,
                Microsoft.Extensions.Logging.EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                try
                {
                    _out.WriteLine($"[{logLevel}] {formatter(state, exception)}");
                    if (exception != null) _out.WriteLine($"    EX: {exception}");
                }
                catch { }
            }
        }
    }
}
