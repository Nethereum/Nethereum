using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC.Eth
{
    public class EthCapabilities : GenericRpcRequestResponseHandlerNoParam<EthCapabilitiesResult>, IEthCapabilities
    {
        public EthCapabilities(IClient client) : base(client, ApiMethods.eth_capabilities.ToString())
        {
        }
    }
}
