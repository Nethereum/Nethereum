using System.Collections.Generic;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Network.Models;

namespace Nethereum.WalletConnect.Requests
{
    [RpcMethod("personal_sign"), RpcRequestOptions(Clock.ONE_MINUTE, 99998)]
    public class WCPersonalSign : List<string>
    {
        public WCPersonalSign(string hexUtf8, string account) : base(new string[] { hexUtf8, account })
        {

        }
        public WCPersonalSign()
        {
        }
    }

}




