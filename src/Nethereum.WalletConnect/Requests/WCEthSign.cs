using System.Collections.Generic;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Network.Models;

namespace Nethereum.WalletConnect.Requests
{
    [RpcMethod("eth_sign"), RpcRequestOptions(Clock.ONE_MINUTE, 99994)]
    public class WCEthSign : List<string>
    {
        public WCEthSign(string account, string hexUtf8) : base(new string[] { account, hexUtf8 })
        {

        }

        public WCEthSign()
        {
        }
    }

}





