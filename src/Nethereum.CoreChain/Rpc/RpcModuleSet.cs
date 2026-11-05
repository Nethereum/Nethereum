using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.CoreChain.Rpc
{
    public sealed class RpcModuleSet
    {
        public const string Eth = "eth";
        public const string Net = "net";
        public const string Web3 = "web3";
        public const string Txpool = "txpool";
        public const string Debug = "debug";
        public const string Admin = "admin";

        public const string DefaultModules = "eth,net,web3";

        private static readonly string[] Known = { Eth, Net, Web3, Txpool, Debug, Admin };

        private readonly HashSet<string> _modules;

        public RpcModuleSet(IEnumerable<string>? modules)
        {
            _modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var module in modules ?? Enumerable.Empty<string>())
            {
                var name = module?.Trim();
                if (string.IsNullOrEmpty(name)) continue;
                if (!Known.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Unknown RPC module '{name}'. Known modules: {string.Join(",", Known)}");
                }

                _modules.Add(name);
            }
        }

        public static RpcModuleSet Default => Parse(DefaultModules);

        public static RpcModuleSet Parse(string? commaSeparated) =>
            new RpcModuleSet((commaSeparated ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

        public bool Contains(string module) => _modules.Contains(module);
    }
}
