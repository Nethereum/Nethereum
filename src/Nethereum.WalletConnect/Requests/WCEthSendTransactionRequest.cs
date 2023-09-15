using Nethereum.WalletConnect.DTOs;
using System.Collections.Generic;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Network.Models;

namespace Nethereum.WalletConnect.Requests
{
    [RpcMethod("eth_sendTransaction"), RpcRequestOptions(Clock.ONE_MINUTE, 99993)]
    public class WCEthSendTransactionRequest : List<WCTransactionInput>
    {
        public WCEthSendTransactionRequest(params WCTransactionInput[] transactions) : base(transactions)
        {
        }

        public WCEthSendTransactionRequest()
        {
        }
    }


}
