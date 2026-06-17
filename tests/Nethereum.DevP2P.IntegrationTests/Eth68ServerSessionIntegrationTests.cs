using System;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Strategies;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    [Collection(DevP2PGethFixture.COLLECTION_NAME)]
    public class Eth68ServerSessionIntegrationTests
    {
        private readonly DevP2PGethFixture _fixture;
        private readonly ITestOutputHelper _output;

        public Eth68ServerSessionIntegrationTests(
            DevP2PGethFixture fixture,
            ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task NethereumRelay_ServesBlocksToNethereumClient_RoundTrip()
        {
            var web3 = _fixture.GetWeb3();
            await _fixture.SendEtherFromSealerAsync(
                "0x27Ef5cDBe01777D62438AfFeb695e33fC2335979",
                BigInteger.Parse("10000000000000000"));
            var targetBlock = (ulong)(await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;

            var devP2PConfig = new DevP2PConfig
            {
                NetworkId = _fixture.NetworkId,
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                RequestTimeoutMs = 10000
            };

            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();

            await using (var upstream = new DevP2PSequencerRpcClient(_fixture.Enode, devP2PConfig, _fixture.GenesisHash))
            {
                for (ulong blockNumber = 0; blockNumber <= targetBlock; blockNumber++)
                {
                    var data = await upstream.GetBlockWithReceiptsAsync(blockNumber);
                    Assert.NotNull(data);
                    await blockStore.SaveAsync(data!.Header, data.BlockHash);
                    for (int i = 0; i < data.Transactions.Count; i++)
                        await txStore.SaveAsync(data.Transactions[i], data.BlockHash, i, (BigInteger)data.Header.BlockNumber);
                    for (int i = 0; i < data.Receipts.Count; i++)
                    {
                        await receiptStore.SaveAsync(
                            data.Receipts[i],
                            data.Transactions[i].Hash,
                            data.BlockHash,
                            (BigInteger)data.Header.BlockNumber,
                            i,
                            (BigInteger)(ulong)data.Receipts[i].CumulativeGasUsed,
                            null,
                            BigInteger.Zero);
                    }
                }
            }
            _output.WriteLine($"Relay primed with blocks 0..{targetBlock}");

            var handler = new StorageBackedEth68Handler(blockStore, txStore, receiptStore);
            var relayKey = EthECKey.GenerateKey();
            var localStatus = new Eth68StatusMessage
            {
                ProtocolVersion = 68,
                NetworkId = _fixture.NetworkId,
                TotalDifficulty = BigInteger.One,
                BestHash = _fixture.GenesisHash,
                GenesisHash = _fixture.GenesisHash,
                ForkHash = ForkId.ComputeHash(_fixture.GenesisHash, Array.Empty<ulong>()),
                ForkNext = 0
            };

            using var sessionCts = new CancellationTokenSource();
            var sessionStarted = new TaskCompletionSource<bool>();

            var listener = new RlpxListener(relayKey, devP2PConfig);
            listener.PeerAccepted += async (_, conn) =>
            {
                var session = new Eth68ServerSession(conn, handler, localStatus);
                try
                {
                    await session.ExchangeStatusAsync(sessionCts.Token);
                    sessionStarted.TrySetResult(true);
                    await session.RunAsync(cancellationToken: sessionCts.Token);
                }
                catch
                {
                    // background session loop: swallow on shutdown.
                    // Writing to ITestOutputHelper here crashes xunit if the test has finished.
                }
            };
            listener.PeerFailed += (_, _) => { };

            try
            {
                listener.Start(port: 0, bindAddress: IPAddress.Loopback);
                var relayEnode = $"enode://{relayKey.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}";
                _output.WriteLine($"Relay listening at {relayEnode}");

                await using var clientToRelay = new DevP2PSequencerRpcClient(relayEnode, devP2PConfig, _fixture.GenesisHash);

                var relayHeader = await clientToRelay.GetBlockHeaderAsync(BigInteger.Zero);
                Assert.NotNull(relayHeader);
                var computed = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(relayHeader);
                Assert.Equal(_fixture.GenesisHash.ToHex(true), computed.ToHex(true));
                _output.WriteLine($"Genesis served by relay matches Geth: {computed.ToHex(true)}");

                var relayBlock = await clientToRelay.GetBlockWithReceiptsAsync(targetBlock);
                Assert.NotNull(relayBlock);
                Assert.True(relayBlock!.Transactions.Count >= 1, $"Expected >=1 tx, got {relayBlock.Transactions.Count}");
                Assert.True(relayBlock.Receipts.Count >= 1, $"Expected >=1 receipt, got {relayBlock.Receipts.Count}");

                _output.WriteLine(
                    $"Block {targetBlock} served by relay: {relayBlock.Transactions.Count} txs, " +
                    $"{relayBlock.Receipts.Count} receipts, hash {relayBlock.BlockHash.ToHex(true)}");
            }
            finally
            {
                sessionCts.Cancel();
                await listener.StopAsync();
            }
        }
    }
}
