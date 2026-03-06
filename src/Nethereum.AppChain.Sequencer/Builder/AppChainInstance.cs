using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Web3;

namespace Nethereum.AppChain.Sequencer.Builder
{
    public class AppChainInstance : IAsyncDisposable
    {
        private readonly ISequencer _sequencer;
        private bool _disposed;

        public AppChainInstance(
            IAppChain appChain,
            ISequencer sequencer,
            AppChainNode node,
            AppChainRpcClient rpcClient,
            IWeb3 web3,
            MudGenesisResult? mudResult = null)
        {
            AppChain = appChain ?? throw new ArgumentNullException(nameof(appChain));
            _sequencer = sequencer ?? throw new ArgumentNullException(nameof(sequencer));
            Sequencer = sequencer;
            Node = node ?? throw new ArgumentNullException(nameof(node));
            RpcClient = rpcClient ?? throw new ArgumentNullException(nameof(rpcClient));
            Web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            MudResult = mudResult;
        }

        public IAppChain AppChain { get; }
        public ISequencer Sequencer { get; }
        public AppChainNode Node { get; }
        public AppChainRpcClient RpcClient { get; }
        public IWeb3 Web3 { get; }
        public MudGenesisResult? MudResult { get; }

        public string ChainName => AppChain.Config.AppChainName;
        public BigInteger ChainId => AppChain.Config.ChainId;
        public string OperatorAddress => Sequencer.Config.SequencerAddress;

        public async Task<BigInteger> GetBlockNumberAsync()
        {
            return await AppChain.GetBlockNumberAsync();
        }

        public async Task<BigInteger> GetBalanceAsync(string address)
        {
            return await AppChain.GetBalanceAsync(address);
        }

        public async Task<byte[]> ProduceBlockAsync()
        {
            return await Sequencer.ProduceBlockAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            await _sequencer.StopAsync();

            GC.SuppressFinalize(this);
        }
    }
}
