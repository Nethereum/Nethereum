
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.Extensions.DevTools.Evm
{

///<Summary>
/// Force a single block to be mined.
/// 
/// Mines a block independent of whether or not mining is started or stopped. Will mine an empty block if there are no available transactions to mine.    
///</Summary>
    public class EvmMine : GenericRpcRequestResponseHandlerNoParam<string>
    {
        public EvmMine(IClient client) : base(client, ApiMethods.evm_mine.ToString()) { }
    }

}
