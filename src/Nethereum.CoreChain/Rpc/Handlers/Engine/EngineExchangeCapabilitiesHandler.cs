using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc.Handlers.Engine
{
    public class EngineExchangeCapabilitiesHandler : RpcHandlerBase
    {
        private readonly RpcHandlerRegistry _registry;

        public EngineExchangeCapabilitiesHandler(RpcHandlerRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public override string MethodName => "engine_exchangeCapabilities";

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var capabilities = _registry.GetAllMethodNames()
                .Where(name => !string.Equals(name, MethodName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            return Task.FromResult(Success(request.Id, capabilities));
        }
    }
}
