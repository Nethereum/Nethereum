

using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC
{

    ///<Summary>
       /// Turns on block profiling for the given duration and writes profile data to disk. It uses a profile rate of 1 for most accurate information. If a different rate is desired, set the rate and write the profile manually using debug_writeBlockProfile.    
    ///</Summary>
    public class DebugBlockProfile : RpcRequestResponseHandler<object>
        {
            public DebugBlockProfile(IClient client) : base(client,ApiMethods.debug_blockProfile.ToString()) { }

            public Task<object> SendRequestAsync(string file, long seconds, object id = null)
            {
                return base.SendRequestAsync(id, file, seconds);
            }
            public RpcRequest BuildRequest(string file, long seconds, object id = null)
            {
                return base.BuildRequest(id, file, seconds);
            }
        }

    }

