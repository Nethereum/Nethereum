using Nethereum.WalletConnect.DTOs;
using System.Collections.Generic;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Network.Models;

namespace Nethereum.WalletConnect.Requests
{
    [RpcMethod("wallet_addEthereumChain"), RpcRequestOptions(Clock.ONE_MINUTE, 99993)]
    public class WCWalletAddEthereumChainRequest : List<WCAddEthereumChainParameter>
    {
        public WCWalletAddEthereumChainRequest(params WCAddEthereumChainParameter[] addEthereumChainParameters) : base(addEthereumChainParameters)
        {
        }

        public WCWalletAddEthereumChainRequest()
        {
        }
    }


}
