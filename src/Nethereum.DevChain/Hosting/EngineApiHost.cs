using Nethereum.CoreChain.Rpc;

namespace Nethereum.DevChain.Hosting
{
    public sealed class EngineApiHost
    {
        public EngineApiHost(RpcHandlerRegistry registry, RpcDispatcher dispatcher, EngineJwtValidator validator)
        {
            Registry = registry;
            Dispatcher = dispatcher;
            Validator = validator;
        }

        public RpcHandlerRegistry Registry { get; }

        public RpcDispatcher Dispatcher { get; }

        public EngineJwtValidator Validator { get; }
    }
}
