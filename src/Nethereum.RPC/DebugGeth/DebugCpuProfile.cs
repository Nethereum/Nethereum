

using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC
{

    ///<Summary>
       /// Turns on CPU profiling for the given duration and writes profile data to disk.    
    ///</Summary>
    public class DebugCpuProfile : RpcRequestResponseHandler<object>
        {
            public DebugCpuProfile(IClient client) : base(client,ApiMethods.debug_cpuProfile.ToString()) { }

            public Task<object> SendRequestAsync(string filePath, int seconds, object id = null)
            {
                return base.SendRequestAsync(id, filePath, seconds);
            }
            public RpcRequest BuildRequest(string filePath, int seconds, object id = null)
            {
                return base.BuildRequest(id, filePath, seconds);
            }
        }

    }

