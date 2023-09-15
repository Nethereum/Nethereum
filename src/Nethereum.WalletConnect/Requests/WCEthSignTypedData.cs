using System.Collections.Generic;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Network.Models;

namespace Nethereum.WalletConnect.Requests
{
    [RpcMethod("eth_signTypedData"), RpcRequestOptions(Clock.ONE_MINUTE, 99996)]
    public class WCEthSignTypedData : List<string>
    {
        public WCEthSignTypedData(string account, string hexUtf8) : base(new string[] { account, hexUtf8 })
        {
        }

        public WCEthSignTypedData()
        {
        }
    }

}
