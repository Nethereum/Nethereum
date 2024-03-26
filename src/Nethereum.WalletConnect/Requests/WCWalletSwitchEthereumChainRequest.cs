using Nethereum.WalletConnect.DTOs;
using System.Collections.Generic;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Network.Models;

namespace Nethereum.WalletConnect.Requests
{
    [RpcMethod("wallet_switchEthereumChain"), RpcRequestOptions(Clock.ONE_MINUTE, 99993)]
    public class WCWalletSwitchEthereumChainRequest : List<WCSwitchEthereumChainParameter>
    {
        public WCWalletSwitchEthereumChainRequest(params WCSwitchEthereumChainParameter[] switchEthereumChainParameters) : base(switchEthereumChainParameters)
        {
        }

        public WCWalletSwitchEthereumChainRequest()
        {
        }
    }


}
