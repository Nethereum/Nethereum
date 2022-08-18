using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Threading.Tasks;
using System;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.HostWallet
{
    /// <summary>
    /// EIP-1102 https://eips.ethereum.org/EIPS/eip-1102
    /// Requests that the user provides an Ethereum address to be identified by.
    /// </summary>
    public class EthRequestAccounts : GenericRpcRequestResponseHandlerNoParam<string[]>, IEthRequestAccounts
    {
        public EthRequestAccounts(IClient client) : base(client, ApiMethods.eth_requestAccounts.ToString())
        {
        }
    }
}
