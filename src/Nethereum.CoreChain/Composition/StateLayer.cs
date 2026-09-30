using System;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM.BlockchainState;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Composition
{
    public class StateLayer
    {
        public StoresFactory Stores { get; }

        public ReadersFactory Readers { get; }

        public StateLayer()
        {
            Stores = new StoresFactory();
            Readers = new ReadersFactory();
        }
    }

    public class StoresFactory
    {
        public IStateStore InMemory()
        {
            return new InMemoryStateStore();
        }

        public IStateStore HistoricalInMemory()
        {
            return new HistoricalStateStore(new InMemoryStateStore());
        }

        public IStateStore HistoricalInMemory(IStateStore rawState, IStateDiffStore diffStore, HistoricalStateOptions journalOptions)
        {
            return journalOptions != null
                ? new HistoricalStateStore(rawState, diffStore, journalOptions)
                : rawState;
        }

        public IStateStore SnapSync(IStateStore rawFlat, INodeBlobStore nodeBlobStore, Func<byte[]> stateRootProvider, bool backfill = true)
        {
            return new TrieFallbackStateStore(rawFlat, nodeBlobStore, stateRootProvider, backfill);
        }

        public IStateStore WitnessCapture(IStateStore store, IStateStore historicalReadAdapter = null)
        {
            return new ReadOnlyStateStoreWrapper(historicalReadAdapter ?? store);
        }
    }

    public class ReadersFactory
    {
        public IStateReader Store(IStateStore stateStore, IBlockStore blockStore = null)
        {
            return new StateStoreNodeDataService(stateStore, blockStore);
        }

        public IStateReader Rpc(IEthApiService ethApiService, BlockParameter currentBlock)
        {
            return new RpcNodeDataService(ethApiService, currentBlock);
        }

        public IStateReader Fork(IStateStore stateStore, IBlockStore blockStore, IEthApiService remoteEthApi, BlockParameter forkBlock)
        {
            return new ForkingNodeDataService(stateStore, blockStore, remoteEthApi, forkBlock);
        }
    }
}
