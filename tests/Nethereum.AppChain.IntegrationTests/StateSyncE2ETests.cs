using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.AppChain.Sync;
using Nethereum.AppChain.Genesis;
using Nethereum.AppChain.Sequencer;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

using AppChainCore = Nethereum.AppChain.AppChain;

namespace Nethereum.AppChain.IntegrationTests
{
    [Collection("Sequential")]
    public class StateSyncE2ETests : IAsyncLifetime, IDisposable
    {
        private readonly ITestOutputHelper _output;
        private AppChainCore? _sequencerChain;
        private AppChainCore? _replicaChain;
        private Sequencer.Sequencer? _sequencer;

        private const string SequencerPrivateKey = "0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
        private const string ReceiverPrivateKey = "0x0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private readonly string _sequencerAddress;
        private readonly string _receiverAddress;
        private static readonly BigInteger ChainId = new BigInteger(420420);
        private static readonly BigInteger InitialBalance = BigInteger.Parse("10000000000000000000000"); // 10000 ETH

        public StateSyncE2ETests(ITestOutputHelper output)
        {
            _output = output;
            var sequencerKey = new EthECKey(SequencerPrivateKey);
            _sequencerAddress = sequencerKey.GetPublicAddress();
            var receiverKey = new EthECKey(ReceiverPrivateKey);
            _receiverAddress = receiverKey.GetPublicAddress();
        }

        public async Task InitializeAsync()
        {
            // Set up sequencer chain
            var sequencerBlockStore = new InMemoryBlockStore();
            var sequencerTxStore = new InMemoryTransactionStore(sequencerBlockStore);
            var sequencerReceiptStore = new InMemoryReceiptStore();
            var sequencerLogStore = new InMemoryLogStore();
            var sequencerStateStore = new InMemoryStateStore();

            var sequencerConfig = AppChainConfig.CreateWithName("SequencerChain", (int)ChainId);
            sequencerConfig.SequencerAddress = _sequencerAddress;

            _sequencerChain = new AppChainCore(
                sequencerConfig,
                sequencerBlockStore,
                sequencerTxStore,
                sequencerReceiptStore,
                sequencerLogStore,
                sequencerStateStore);

            var genesisOptions = new GenesisOptions
            {
                PrefundedAddresses = new[] { _sequencerAddress },
                PrefundBalance = InitialBalance,
                DeployCreate2Factory = false
            };
            await _sequencerChain.InitializeAsync(genesisOptions);

            // Set up replica chain (will be used with state sync)
            // Note: We initialize with the same genesis options because:
            // 1. Genesis state (prefunded accounts) must exist before re-executing transactions
            // 2. In real sync scenarios, genesis state would be synced separately or bootstrapped
            var replicaBlockStore = new InMemoryBlockStore();
            var replicaTxStore = new InMemoryTransactionStore(replicaBlockStore);
            var replicaReceiptStore = new InMemoryReceiptStore();
            var replicaLogStore = new InMemoryLogStore();
            var replicaStateStore = new InMemoryStateStore();

            var replicaConfig = AppChainConfig.CreateWithName("ReplicaChain", (int)ChainId);

            _replicaChain = new AppChainCore(
                replicaConfig,
                replicaBlockStore,
                replicaTxStore,
                replicaReceiptStore,
                replicaLogStore,
                replicaStateStore);

            // Initialize replica with same genesis state
            await _replicaChain.InitializeAsync(genesisOptions);
        }

        public Task DisposeAsync()
        {
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _sequencer = null;
            _sequencerChain = null;
            _replicaChain = null;
        }

        private (MultiPeerSyncService sync, PeerManager peerManager) CreateSyncService(
            InProcessSequencerRpcClient mockRpcClient,
            InMemoryFinalityTracker finalityTracker,
            IBlockReExecutor? blockReExecutor = null)
        {
            var peerManager = new PeerManager(clientFactory: url => mockRpcClient);
            peerManager.AddPeer("http://localhost:8545");
            var peers = peerManager.Peers.ToList();
            peers[0].IsHealthy = true;

            var config = new MultiPeerSyncConfig
            {
                AutoFollow = false,
                RejectOnStateRootMismatch = false
            };

            var sync = new MultiPeerSyncService(
                config,
                _replicaChain!.Blocks,
                _replicaChain.Transactions,
                _replicaChain.Receipts,
                _replicaChain.Logs,
                finalityTracker,
                peerManager,
                blockReExecutor);

            return (sync, peerManager);
        }

        [Fact]
        public async Task E2E_StateSync_BalancesAvailableOnReplica()
        {
            _output.WriteLine($"Sequencer address: {_sequencerAddress}");
            _output.WriteLine($"Receiver address: {_receiverAddress}");

            // Start sequencer
            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _sequencerAddress,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = Nethereum.AppChain.Sequencer.PolicyConfig.OpenAccess
            };

            _sequencer = new Sequencer.Sequencer(_sequencerChain!, sequencerConfig);
            await _sequencer.StartAsync();

            // Check initial balance on sequencer
            var sequencerInitialBalance = await _sequencerChain!.GetBalanceAsync(_sequencerAddress);
            _output.WriteLine($"Sequencer initial balance: {sequencerInitialBalance}");
            Assert.Equal(InitialBalance, sequencerInitialBalance);

            // Send 5 ETH transfers to receiver
            var transferAmount = BigInteger.Parse("1000000000000000000"); // 1 ETH
            var gasPrice = new BigInteger(1000000000); // 1 gwei
            var gasLimit = new BigInteger(21000);

            for (int i = 0; i < 5; i++)
            {
                var tx = CreateEthTransfer(nonce: i, to: _receiverAddress, value: transferAmount);
                await _sequencer.SubmitTransactionAsync(tx);
                _output.WriteLine($"Submitted transfer {i + 1}: 1 ETH to {_receiverAddress}");
            }

            var sequencerHeight = await _sequencer.GetBlockNumberAsync();
            _output.WriteLine($"Sequencer height after transfers: {sequencerHeight}");
            Assert.Equal(5, sequencerHeight);

            // Verify balances on sequencer
            var sequencerSenderBalance = await _sequencerChain.GetBalanceAsync(_sequencerAddress);
            var sequencerReceiverBalance = await _sequencerChain.GetBalanceAsync(_receiverAddress);
            _output.WriteLine($"Sequencer - Sender balance: {sequencerSenderBalance}");
            _output.WriteLine($"Sequencer - Receiver balance: {sequencerReceiverBalance}");

            // Expected: 5 ETH transferred + gas costs
            var expectedReceiverBalance = transferAmount * 5;
            Assert.Equal(expectedReceiverBalance, sequencerReceiverBalance);

            // Now sync to replica WITH state re-execution
            var mockRpcClient = new InProcessSequencerRpcClient(_sequencerChain);
            var finalityTracker = new InMemoryFinalityTracker();

            // Create BlockReExecutor for state sync
            var chainConfig = new ChainConfig
            {
                ChainId = ChainId,
                BaseFee = BigInteger.Zero,
                Coinbase = _sequencerAddress
            };

            var txVerifier = new TransactionVerificationAndRecoveryImp();

            var transactionProcessor = new TransactionProcessor(
                _replicaChain!.State,
                _replicaChain.Blocks,
                chainConfig,
                txVerifier,
                chainConfig.GetHardforkConfig());

            var blockReExecutor = new BlockReExecutor(
                transactionProcessor,
                _replicaChain.State,
                chainConfig);

            var (liveSync, peerManager) = CreateSyncService(mockRpcClient, finalityTracker, blockReExecutor);
            peerManager.Peers.First().BlockNumber = 5;

            // Subscribe to state root mismatch events
            liveSync.StateRootMismatch += (sender, args) =>
            {
                _output.WriteLine($"State root mismatch at block {args.BlockNumber}:");
                _output.WriteLine($"  Expected: {args.ExpectedStateRoot?.ToHexCompact() ?? "null"}");
                _output.WriteLine($"  Computed: {args.ComputedStateRoot?.ToHexCompact() ?? "null"}");
            };

            await liveSync.StartAsync();
            var result = await liveSync.SyncToLatestAsync();

            _output.WriteLine($"Sync result: Success={result.Success}, Blocks={result.BlocksSynced}");
            Assert.True(result.Success);
            Assert.Equal(5, result.BlocksSynced); // Replica already has genesis, sync blocks 1-5

            var replicaHeight = await _replicaChain.Blocks.GetHeightAsync();
            _output.WriteLine($"Replica height: {replicaHeight}");
            Assert.Equal(5, replicaHeight);

            // Verify balances on replica
            var replicaSenderBalance = await _replicaChain.GetBalanceAsync(_sequencerAddress);
            var replicaReceiverBalance = await _replicaChain.GetBalanceAsync(_receiverAddress);
            _output.WriteLine($"Replica - Sender balance: {replicaSenderBalance}");
            _output.WriteLine($"Replica - Receiver balance: {replicaReceiverBalance}");

            // The key assertion: receiver balance should be available on replica
            Assert.True(replicaReceiverBalance > 0, "Replica should have receiver balance after state sync");
            Assert.Equal(expectedReceiverBalance, replicaReceiverBalance);

            await liveSync.StopAsync();
            await _sequencer.StopAsync();
        }

        [Fact]
        public async Task E2E_StateSync_WithoutReExecutor_NoBalancesOnReplica()
        {
            _output.WriteLine("Testing sync WITHOUT BlockReExecutor - balances should NOT be available");

            // Start sequencer
            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _sequencerAddress,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = Nethereum.AppChain.Sequencer.PolicyConfig.OpenAccess
            };

            _sequencer = new Sequencer.Sequencer(_sequencerChain!, sequencerConfig);
            await _sequencer.StartAsync();

            // Send 3 ETH transfers
            var transferAmount = BigInteger.Parse("1000000000000000000"); // 1 ETH
            for (int i = 0; i < 3; i++)
            {
                var tx = CreateEthTransfer(nonce: i, to: _receiverAddress, value: transferAmount);
                await _sequencer.SubmitTransactionAsync(tx);
            }

            // Verify balances on sequencer
            var sequencerReceiverBalance = await _sequencerChain!.GetBalanceAsync(_receiverAddress);
            Assert.Equal(transferAmount * 3, sequencerReceiverBalance);

            // Sync to replica WITHOUT state re-execution
            var mockRpcClient = new InProcessSequencerRpcClient(_sequencerChain);
            var finalityTracker = new InMemoryFinalityTracker();

            // No BlockReExecutor passed
            var (liveSync, peerManager) = CreateSyncService(mockRpcClient, finalityTracker, blockReExecutor: null);
            peerManager.Peers.First().BlockNumber = 3;

            await liveSync.StartAsync();
            var result = await liveSync.SyncToLatestAsync();

            Assert.True(result.Success);

            // Check balance on replica - should be ZERO because no state was built
            var replicaReceiverBalance = await _replicaChain!.GetBalanceAsync(_receiverAddress);
            _output.WriteLine($"Replica receiver balance (no re-execution): {replicaReceiverBalance}");

            // Without re-execution, balance should be 0
            Assert.Equal(BigInteger.Zero, replicaReceiverBalance);

            await liveSync.StopAsync();
            await _sequencer.StopAsync();
        }

        [Fact]
        public async Task E2E_StateSync_IncrementalSync_MaintainsState()
        {
            _output.WriteLine("Testing incremental sync maintains state across multiple sync batches");

            // Start sequencer
            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _sequencerAddress,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = Nethereum.AppChain.Sequencer.PolicyConfig.OpenAccess
            };

            _sequencer = new Sequencer.Sequencer(_sequencerChain!, sequencerConfig);
            await _sequencer.StartAsync();

            // Set up replica with BlockReExecutor
            var mockRpcClient = new InProcessSequencerRpcClient(_sequencerChain!);
            var finalityTracker = new InMemoryFinalityTracker();

            var chainConfig = new ChainConfig
            {
                ChainId = ChainId,
                BaseFee = BigInteger.Zero,
                Coinbase = _sequencerAddress
            };

            var txVerifier = new TransactionVerificationAndRecoveryImp();
            var transactionProcessor = new TransactionProcessor(
                _replicaChain!.State,
                _replicaChain.Blocks,
                chainConfig,
                txVerifier,
                chainConfig.GetHardforkConfig());

            var blockReExecutor = new BlockReExecutor(
                transactionProcessor,
                _replicaChain.State,
                chainConfig);

            var (liveSync, peerManager) = CreateSyncService(mockRpcClient, finalityTracker, blockReExecutor);

            await liveSync.StartAsync();

            // First batch: 3 transfers
            var transferAmount = BigInteger.Parse("1000000000000000000"); // 1 ETH
            for (int i = 0; i < 3; i++)
            {
                var tx = CreateEthTransfer(nonce: i, to: _receiverAddress, value: transferAmount);
                await _sequencer.SubmitTransactionAsync(tx);
            }

            // Sync first batch
            peerManager.Peers.First().BlockNumber = 3;
            var result1 = await liveSync.SyncToLatestAsync();
            Assert.True(result1.Success);
            _output.WriteLine($"First sync: {result1.BlocksSynced} blocks");

            var replicaBalance1 = await _replicaChain.GetBalanceAsync(_receiverAddress);
            _output.WriteLine($"Replica balance after first sync: {replicaBalance1}");
            Assert.Equal(transferAmount * 3, replicaBalance1);

            // Second batch: 3 more transfers
            for (int i = 3; i < 6; i++)
            {
                var tx = CreateEthTransfer(nonce: i, to: _receiverAddress, value: transferAmount);
                await _sequencer.SubmitTransactionAsync(tx);
            }

            // Sync second batch
            peerManager.Peers.First().BlockNumber = 6;
            var result2 = await liveSync.SyncToLatestAsync();
            Assert.True(result2.Success);
            _output.WriteLine($"Second sync: {result2.BlocksSynced} blocks");

            var replicaBalance2 = await _replicaChain.GetBalanceAsync(_receiverAddress);
            _output.WriteLine($"Replica balance after second sync: {replicaBalance2}");
            Assert.Equal(transferAmount * 6, replicaBalance2);

            await liveSync.StopAsync();
            await _sequencer.StopAsync();
        }

        private ISignedTransaction CreateEthTransfer(int nonce, string to, BigInteger value)
        {
            var privateKey = new EthECKey(SequencerPrivateKey);

            var transaction = new Transaction1559(
                chainId: ChainId,
                nonce: nonce,
                maxPriorityFeePerGas: BigInteger.Zero,
                maxFeePerGas: new BigInteger(1000000000),
                gasLimit: new BigInteger(21000),
                receiverAddress: to,
                amount: value,
                data: null,
                accessList: null
            );

            var signature = privateKey.SignAndCalculateYParityV(transaction.RawHash);
            transaction.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });

            return transaction;
        }
    }

    public static class ByteArrayExtensions
    {
        public static string ToHexCompact(this byte[] bytes)
        {
            return "0x" + BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
