using System.Collections.Generic;
using Nethereum.CoreChain.Rpc;

namespace Nethereum.ChainNode.Hosting.Rpc
{
    public sealed class RpcHttpOptions
    {
        public long MaxRequestBodySize { get; set; } = 10 * 1024 * 1024;

        public long MaxResponseSize { get; set; } = JsonRpcEnvelope.DefaultMaxResponseSize;

        public string? BindHost { get; set; }

        public IList<string> AllowedHosts { get; set; } = new List<string>();

        public IList<string> AllowedOrigins { get; set; } = new List<string>();

        public RpcHostPolicy HostPolicy => new RpcHostPolicy(AllowedHosts, BindHost);

        public RpcOriginPolicy OriginPolicy => new RpcOriginPolicy(AllowedOrigins);
    }
}
