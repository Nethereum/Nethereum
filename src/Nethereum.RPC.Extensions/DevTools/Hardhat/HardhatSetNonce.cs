
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.Extensions.DevTools.Hardhat
{

///<Summary>
/// Modifies an account's nonce by overwriting it    
///</Summary>
    public class HardhatSetNonce : RpcRequestResponseHandler<string>
    {
        public HardhatSetNonce(IClient client) : base(client,ApiMethods.hardhat_setNonce.ToString()) { }

        public Task SendRequestAsync(string address, HexBigInteger nonce, object id = null)
        {
            return base.SendRequestAsync(id, address, nonce);
        }
        public RpcRequest BuildRequest(string address, HexBigInteger nonce, object id = null)
        {
            return base.BuildRequest(id, address, nonce);
        }
    }

}

