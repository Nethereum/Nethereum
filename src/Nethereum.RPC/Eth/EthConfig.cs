using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC.Eth
{
    /// <Summary>
    ///    eth_config
    ///    EIP-7910. Returns the client's current configuration including fork information:
    ///    the fork it runs now, the one before it, and the next scheduled one with the time
    ///    it activates. Takes no parameters.
    /// </Summary>
    public class EthConfig : GenericRpcRequestResponseHandlerNoParam<ChainConfiguration>, IEthConfig
    {
        public EthConfig(IClient client) : base(client, ApiMethods.eth_config.ToString())
        {
        }
    }
}
