using System;
using System.Threading.Tasks;
 
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Eth
{
    /// <Summary>
    ///     eth_sign
    ///     Signs data with a given address.
    ///     Note the address to sign must be unlocked.
    ///     Parameters
    ///     DATA, 20 Bytes - address
    ///     DATA, Data to sign
    ///     Returns
    ///     DATA: Signed data
    ///     Example
    ///     Request
    ///     curl -X POST --data '{"jsonrpc":"2.0","method":"eth_sign","params":["0xd1ade25ccd3d550a7eb532ac759cac7be09c2719",
    ///     "0xdeadbeef"],"id":1}'
    ///     Result
    ///     {
    ///     "id":1,
    ///     "jsonrpc": "2.0",
    ///     "result":
    ///     "0x2ac19db245478a06032e69cdbd2b54e648b78431d0a47bd1fbab18f79f820ba407466e37adbe9e84541cab97ab7d290f4a64a5825c876d22109f3bf813254e8601"
    ///     }
    /// </Summary>
    public class EthSign : RpcRequestResponseHandler<string>, IEthSign
    {
        public EthSign(IClient client) : base(client, ApiMethods.eth_sign.ToString())
        {
        }

        public Task<string> SendRequestAsync(string address, string data, object id = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (data == null) throw new ArgumentNullException(nameof(data));
            return base.SendRequestAsync(id, address.EnsureHexPrefix(), data);
        }

        public RpcRequest BuildRequest(string address, string data, object id = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (data == null) throw new ArgumentNullException(nameof(data));
            return base.BuildRequest(id, address.EnsureHexPrefix(), data);
        }
    }
}