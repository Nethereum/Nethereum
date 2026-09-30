using System;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    public class Snap1BootstrapE2ETests
    {
        private readonly ITestOutputHelper _output;

        public Snap1BootstrapE2ETests(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task Given_PeersThatDoNotAdvertiseSnap2_When_TheyConnect_Then_SnapVersion1IsNegotiated()
        {
            var producerNode = DevChainNode.CreateWithRocksDb(advertiseSnap2: false);
            var joiner = DevChainNode.CreateWithRocksDb(advertiseSnap2: false);
            try
            {
                await producerNode.StartAsync();
                await joiner.StartAsync();
                await joiner.ConnectToAsync(producerNode);

                var session = joiner.OutboundSessionTo(producerNode);
                Assert.NotNull(session);

                var snap = session.Connection.SharedCapabilities.Find(c => c.Name == "snap");
                Assert.NotNull(snap);
                Assert.Equal(1, snap.Version);
                Assert.True(session.SupportsSnap);

                _output.WriteLine($"negotiated snap/{snap.Version}");
            }
            finally
            {
                await joiner.DisposeAsync();
                await producerNode.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_AChainUnderLoad_When_AColdNodeSnapSyncsOverSnap1_Then_ItCompletesEveryPhaseAndTheStateMatches()
        {
            var producerNode = DevChainNode.CreateWithRocksDb(advertiseSnap2: false);
            var joiner = DevChainNode.CreateWithRocksDb(advertiseSnap2: false);
            try
            {
                await producerNode.StartAsync();
                var producer = await DevChainProducer.AttachAsync(producerNode);

                var load = await DevChainLoad.GenerateAsync(producerNode, producer, blocks: 80, contracts: 4);
                _output.WriteLine($"load: height={load.Height} txs={load.Transactions} contracts={load.ContractAddresses.Count}");

                await joiner.StartAsync();
                await joiner.ConnectToAsync(producerNode);

                var session = joiner.OutboundSessionTo(producerNode);
                Assert.Equal(1, session.Connection.SharedCapabilities.Find(c => c.Name == "snap").Version);

                var follower = await DevChainFollower.AttachAsync(joiner);
                try
                {
                    using var deadline = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(4));
                    var result = await follower.SnapBootstrapAsync(deadline.Token);

                    Assert.True(result.Ran, $"snap/1 bootstrap did not run: {result.SkipReason}");
                    _output.WriteLine(
                        $"snap/1 pivot={result.PivotBlockNumber} accounts={result.AccountCount} " +
                        $"slots={result.SlotCount} bytecode={result.BytecodeCount}");

                    Assert.True(result.AccountCount > 0, "no accounts synced over snap/1");
                    Assert.True(result.SlotCount > 0, "no storage slots synced over snap/1");
                    Assert.True(result.BytecodeCount > 0, "no bytecode synced over snap/1");

                    var pivot = (long)result.PivotBlockNumber;
                    var expectedRoot = (await producerNode.Bundle.Blocks.GetByNumberAsync(pivot))?.StateRoot;
                    Assert.True(ByteUtil.AreEqual(result.PivotStateRoot, expectedRoot),
                        "snap/1 pivot state root does not match the producer's");

                    foreach (var address in load.ContractAddresses)
                    {
                        var key = address.ToLowerInvariant();
                        var mine = await joiner.Bundle.State.GetAccountAsync(key);
                        var theirs = await producerNode.Bundle.State.GetAccountAsync(key);
                        Assert.NotNull(mine);

                        Assert.True(ByteUtil.AreEqual(mine.CodeHash, theirs.CodeHash),
                            $"code hash differs for {address} over snap/1");

                        var myCode = await joiner.Bundle.State.GetCodeAsync(mine.CodeHash);
                        var theirCode = await producerNode.Bundle.State.GetCodeAsync(theirs.CodeHash);
                        Assert.True(myCode != null && myCode.Length > 0, $"no code synced for {address} over snap/1");
                        Assert.True(ByteUtil.AreEqual(myCode, theirCode), $"code bytes differ for {address} over snap/1");

                        for (var slot = 0; slot < 3; slot++)
                        {
                            var mySlot = await joiner.Bundle.State.GetStorageAsync(key, slot);
                            var theirSlot = await producerNode.Bundle.State.GetStorageAsync(key, slot);
                            Assert.True(ByteUtil.AreEqual(mySlot, theirSlot),
                                $"storage slot {slot} differs for {address} over snap/1");
                        }
                    }

                    _output.WriteLine($"snap/1 synced to pivot {pivot}: code and storage match for {load.ContractAddresses.Count} contracts");
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
    }
}
