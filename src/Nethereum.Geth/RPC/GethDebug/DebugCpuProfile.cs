using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Debug
{
    /// <Summary>
    ///     Turns on CPU profiling for the given duration and writes profile data to disk.
    /// </Summary>
    public class DebugCpuProfile : RpcRequestResponseHandler<object>
    {
        public DebugCpuProfile(IClient client) : base(client, ApiMethods.debug_cpuProfile.ToString())
        {
        }

        public RpcRequest BuildRequest(string filePath, int seconds, object id = null)
        {
            return base.BuildRequest(id, filePath, seconds);
        }

        public Task<object> SendRequestAsync(string filePath, int seconds, object id = null)
        {
            return base.SendRequestAsync(id, filePath, seconds);
        }
    }
}