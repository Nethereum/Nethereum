using System;
using System.Linq;
using Nethereum.ChainNode.Hosting.Rpc;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Rpc;

namespace Nethereum.ChainNode.Hosting.Configuration
{
    public static class ChainNodeRpcConfigExtensions
    {
        public static void ApplyTo(this ChainNodeRpcConfig rpc, ChainConfig target)
        {
            target.RpcMaxLogBlockRange = rpc.MaxLogBlockRange;
            target.RpcMaxLogResults = rpc.MaxLogResults;
            target.RpcGasCap = rpc.GasCap;
        }

        public static RpcModuleSet ToModuleSet(this ChainNodeRpcConfig rpc) => RpcModuleSet.Parse(rpc.Modules);

        public static RpcHttpOptions ToHttpOptions(this ChainNodeRpcConfig rpc) =>
            new RpcHttpOptions
            {
                BindHost = rpc.Host,
                AllowedHosts = SplitList(rpc.AllowedHosts),
                AllowedOrigins = SplitList(rpc.AllowedOrigins)
            };

        public static System.Collections.Generic.List<string> SplitList(string? commaSeparated) =>
            (commaSeparated ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Trim())
                .Where(entry => entry.Length > 0)
                .ToList();
    }
}
