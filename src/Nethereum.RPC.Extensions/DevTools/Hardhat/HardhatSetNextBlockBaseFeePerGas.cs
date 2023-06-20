
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.Extensions.DevTools.Hardhat
{

///<Summary>
/// Sets the coinbase address to be used in new blocks    
///</Summary>
    public class HardhatSetNextBlockBaseFeePerGas : RpcRequestResponseHandler<string>
    {
        public HardhatSetNextBlockBaseFeePerGas(IClient client) : base(client,ApiMethods.hardhat_setNextBlockBaseFeePerGas.ToString()) { }

        public Task SendRequestAsync(HexBigInteger baseFeePerGas, object id = null)
        {
            return base.SendRequestAsync(id, baseFeePerGas);
        }
        public RpcRequest BuildRequest(HexBigInteger baseFeePerGas, object id = null)
        {
            return base.BuildRequest(id, baseFeePerGas);
        }
    }

}

