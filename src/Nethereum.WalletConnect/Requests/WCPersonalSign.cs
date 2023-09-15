using System.Collections.Generic;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Network.Models;

namespace Nethereum.WalletConnect.Requests
{
    [RpcMethod("personal_sign"), RpcRequestOptions(Clock.ONE_MINUTE, 99998)]
    public class WCPersonalSign : List<string>
    {
        public WCPersonalSign(string account, string hexUtf8) : base(new string[] { account, hexUtf8 })
        {

        }
        public WCPersonalSign()
        {
        }
    }

}




