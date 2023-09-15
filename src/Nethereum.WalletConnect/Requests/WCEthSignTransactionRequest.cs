using Nethereum.WalletConnect.DTOs;
using System.Collections.Generic;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Network.Models;

namespace Nethereum.WalletConnect.Requests
{
    [RpcMethod("eth_signTransaction"), RpcRequestOptions(Clock.ONE_MINUTE, 99995)]
    public class WCEthSignTransactionRequest : List<WCTransactionInput>
    {
        public WCEthSignTransactionRequest(params WCTransactionInput[] transactions) : base(transactions)
        {
        }

        public WCEthSignTransactionRequest() { }
    }
}





