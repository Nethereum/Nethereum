using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Sync;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests
{
    public class HttpClientSyncTest
    {
        private readonly ITestOutputHelper _output;
        private const string SequencerUrl = "http://127.0.0.1:8560";

        public HttpClientSyncTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact(Skip = "Manual test - requires running sequencer on port 8560")]
        public async Task HttpClient_CanFetchBlockFromSequencer()
        {
            var client = new HttpSequencerRpcClient(SequencerUrl);

            _output.WriteLine("Getting block number...");
            var blockNumber = await client.GetBlockNumberAsync();
            _output.WriteLine($"Block number: {blockNumber}");

            Assert.True(blockNumber > 0, "Sequencer should have blocks");

            _output.WriteLine("Getting block 1...");
            var block1 = await client.GetBlockWithReceiptsAsync(1);

            Assert.NotNull(block1);
            _output.WriteLine($"Block 1 number: {block1.Header.BlockNumber}");
            _output.WriteLine($"Block 1 tx count: {block1.Transactions.Count}");
            _output.WriteLine($"Block 1 receipt count: {block1.Receipts.Count}");

            if (block1.Transactions.Count > 0)
            {
                var tx = block1.Transactions[0];
                _output.WriteLine($"First tx hash: {(tx.Hash != null ? tx.Hash.ToHex(true) : "null")}");
                _output.WriteLine($"First tx type: {tx.TransactionType}");
            }
        }

        [Fact(Skip = "Manual test - requires running sequencer on port 8560")]
        public async Task HttpSync_CanSyncBlocksFromSequencer()
        {
            var client = new HttpSequencerRpcClient(SequencerUrl);

            // Create in-memory stores for follower
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var finalityTracker = new InMemoryFinalityTracker();

            // Create peer manager with the sequencer as peer
            var peerManager = new PeerManager(clientFactory: url => new HttpSequencerRpcClient(url));
            peerManager.AddPeer(SequencerUrl);

            // Create sync config without state re-execution
            var syncConfig = new MultiPeerSyncConfig
            {
                PollIntervalMs = 100,
                AutoFollow = false,
                RejectOnStateRootMismatch = false
            };

            var syncService = new MultiPeerSyncService(
                syncConfig,
                blockStore,
                txStore,
                receiptStore,
                logStore,
                finalityTracker,
                peerManager,
                null // No block re-executor
            );

            await syncService.StartAsync();

            _output.WriteLine("Starting sync...");
            var result = await syncService.SyncToLatestAsync();

            _output.WriteLine($"Sync result: Success={result.Success}, BlocksSynced={result.BlocksSynced}, Error={result.ErrorMessage}");

            Assert.True(result.Success, $"Sync should succeed: {result.ErrorMessage}");
            Assert.True(result.BlocksSynced > 0, "Should have synced some blocks");

            // Verify blocks are in store
            var localHeight = await blockStore.GetHeightAsync();
            _output.WriteLine($"Local height after sync: {localHeight}");
            Assert.True(localHeight > 0, "Local height should be > 0 after sync");

            await syncService.StopAsync();
        }
    }
}
