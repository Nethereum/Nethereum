using Nethereum.CoreChain.Rpc.Handlers.Engine;

namespace Nethereum.CoreChain.Rpc
{
    public static class EngineRpcHandlerExtensions
    {
        public static RpcHandlerRegistry AddEngineHandlers(this RpcHandlerRegistry registry)
        {
            registry.Register(new EngineNewPayloadV3Handler());
            registry.Register(new EngineForkchoiceUpdatedV3Handler());
            registry.Register(new EngineGetPayloadV3Handler());
            registry.Register(new EngineNewPayloadV4Handler());
            registry.Register(new EngineForkchoiceUpdatedV4Handler());
            registry.Register(new EngineGetPayloadV4Handler());
            registry.Register(new EngineNewPayloadV5Handler());
            registry.Register(new EngineGetPayloadV6Handler());
            registry.Register(new EngineGetClientVersionV1Handler());
            registry.Register(new EngineExchangeCapabilitiesHandler(registry));

            return registry;
        }
    }
}
