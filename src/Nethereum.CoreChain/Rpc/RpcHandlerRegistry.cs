using System;
using System.Collections.Generic;

namespace Nethereum.CoreChain.Rpc
{
    public class RpcHandlerRegistry
    {
        private readonly Dictionary<string, IRpcHandler> _handlers = new Dictionary<string, IRpcHandler>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public void Register(IRpcHandler handler)
        {
            _handlers[handler.MethodName] = handler;
        }

        public void Override(IRpcHandler handler)
        {
            _handlers[handler.MethodName] = handler;
        }

        public void RegisterAlias(string aliasMethod, string targetMethod)
        {
            _aliases[aliasMethod] = targetMethod;
        }

        public IRpcHandler GetHandler(string methodName)
        {
            if (_aliases.TryGetValue(methodName, out var targetMethod))
            {
                methodName = targetMethod;
            }

            return _handlers.TryGetValue(methodName, out var handler) ? handler : null;
        }

        public IEnumerable<IRpcHandler> GetAllHandlers() => _handlers.Values;

        public IEnumerable<string> GetAllMethodNames() => _handlers.Keys;

        public int Count => _handlers.Count;
    }
}
