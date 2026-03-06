using System.Numerics;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.AppChain.Sync;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Sequencer.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.AppChain.Sync.UnitTests
{
    public class ReplicaSyncStatusHandlerTests
    {
        private readonly BigInteger _chainId = 420420;

        [Fact]
        public void MethodName_ReturnsCorrectValue()
        {
            var handler = new ReplicaSyncStatusHandler();
            Assert.Equal("replica_syncStatus", handler.MethodName);
        }

        [Fact]
        public async Task HandleAsync_ReturnsReplicaStatus_WhenNodeIsReplica()
        {
            var appChain = await CreateAppChainAsync();
            var txProxy = new MockSequencerTxProxy();
            var config = AppChainReplicaConfig.Default;

            using var replica = new AppChainReplicaNode(appChain, txProxy, config);

            var handler = new ReplicaSyncStatusHandler();
            var request = new RpcRequestMessage(1, "replica_syncStatus");
            var context = new RpcContext(replica, _chainId, new MockServiceProvider());

            var response = await handler.HandleAsync(request, context);

            Assert.NotNull(response.Result);
            var status = response.GetResultSTJ<ReplicaSyncStatus>();
            Assert.NotNull(status);
            Assert.True(status.IsReplica);
            Assert.False(status.Syncing);
            Assert.Equal("Idle", status.SyncMode);
        }

        [Fact]
        public void ReplicaRpcHandlerExtensions_AddReplicaHandlers_RegistersHandler()
        {
            var registry = new RpcHandlerRegistry();
            registry.AddReplicaHandlers();

            var handlers = registry.GetAllHandlers();
            Assert.Contains(handlers, h => h.MethodName == "replica_syncStatus");
        }

        private async Task<IAppChain> CreateAppChainAsync()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var stateStore = new InMemoryStateStore();

            var config = new AppChainConfig { ChainId = _chainId };
            var appChain = new Nethereum.AppChain.AppChain(config, blockStore, txStore, receiptStore, logStore, stateStore);
            await appChain.InitializeAsync();

            return appChain;
        }
    }

    public class MockServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
