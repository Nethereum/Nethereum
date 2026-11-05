using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain;
using Nethereum.DevChain.Hosting;

namespace Nethereum.Node.HarnessServer
{
    public sealed class HarnessNode : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly DevChainComposedNode _composed;

        internal HarnessNode(ServiceProvider provider, DevChainComposedNode composed, BlockImporter importer)
        {
            _provider = provider;
            _composed = composed;
            Importer = importer;
        }

        public DevChainNode Node => _composed.Node;
        public IChainStoreBundle Stores => _composed.Bundle;
        public BlockImporter Importer { get; }
        public RpcDispatcher Dispatcher => _provider.GetRequiredService<RpcDispatcher>();
        public EngineApiService Engine => (EngineApiService)_provider.GetRequiredService<IEngineApiService>();
        public RpcDispatcher EngineDispatcher => new RpcDispatcher(
            new RpcHandlerRegistry().AddEngineHandlers(), new RpcContext(Node, Node.Config.ChainId, _provider));

        public async ValueTask DisposeAsync()
        {
            await _composed.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
