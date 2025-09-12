using System.Collections.Generic;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Wallet.UI;
using System.Threading.Tasks;

namespace Nethereum.Wallet.Hosting
{
    public interface IRpcMethodHandler
    {
        string MethodName { get; }
        Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context);
    }
    
    public class RpcHandlerRegistry
    {
        private readonly Dictionary<string, IRpcMethodHandler> _handlers = new();

        public void Register(IRpcMethodHandler handler) => _handlers[handler.MethodName] = handler;
        public void RegisterHandler(string method, IRpcMethodHandler handler) => _handlers[method] = handler;
        public bool TryGetHandler(string method, out IRpcMethodHandler? handler) => _handlers.TryGetValue(method, out handler);
        public IRpcMethodHandler? GetHandler(string method) => _handlers.TryGetValue(method, out var handler) ? handler : null;
    }
}