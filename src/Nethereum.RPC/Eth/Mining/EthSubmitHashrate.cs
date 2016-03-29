using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Eth.Mining
{
    /// <Summary>
    ///     eth_submitHashrate
    ///     Used for submitting mining hashrate.
    ///     Parameters
    ///     Hashrate, a hexadecimal string representation (32 bytes) of the hash rate
    ///     ID, String - A random hexadecimal(32 bytes) ID identifying the client
    ///     params: [
    ///     "0x0000000000000000000000000000000000000000000000000000000000500000",
    ///     "0x59daa26581d0acd1fce254fb7e85952f4c09d0915afd33d3886cd914bc7d283c"
    ///     ]
    ///     Returns
    ///     Boolean - returns true if submitting went through succesfully and false otherwise.
    ///     Example
    ///     Request
    ///     curl -X POST --data '{"jsonrpc":"2.0", "method":"eth_submitHashrate",
    ///     "params":["0x0000000000000000000000000000000000000000000000000000000000500000",
    ///     "0x59daa26581d0acd1fce254fb7e85952f4c09d0915afd33d3886cd914bc7d283c"],"id":73}'
    ///     Result
    ///     {
    ///     "id":73,
    ///     "jsonrpc":"2.0",
    ///     "result": true
    ///     }
    /// </Summary>
    public class EthSubmitHashrate : RpcRequestResponseHandler<bool>
    {
        public EthSubmitHashrate(IClient client) : base(client, ApiMethods.eth_submitHashrate.ToString())
        {
        }

        public async Task<bool> SendRequestAsync(string[] hashRateAndId, object id = null)
        {
            return await base.SendRequestAsync(id, hashRateAndId);
        }

        public RpcRequest BuildRequest(string[] hashRateAndId, object id = null)
        {
            return base.BuildRequest(id, hashRateAndId);
        }
    }
}