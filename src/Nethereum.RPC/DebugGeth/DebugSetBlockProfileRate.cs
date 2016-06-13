

using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC
{

    ///<Summary>
       /// Sets the rate (in samples/sec) of goroutine block profile data collection. A non-zero rate enables block profiling, setting it to zero stops the profile. Collected profile data can be written using debug_writeBlockProfile.    
    ///</Summary>
    public class DebugSetBlockProfileRate : RpcRequestResponseHandler<object>
        {
            public DebugSetBlockProfileRate(IClient client) : base(client,ApiMethods.debug_setBlockProfileRate.ToString()) { }

            public Task<object> SendRequestAsync(long rate, object id = null)
            {
                return base.SendRequestAsync(id, rate);
            }
            public RpcRequest BuildRequest(long rate, object id = null)
            {
                return base.BuildRequest(id, rate);
            }
        }

    }

