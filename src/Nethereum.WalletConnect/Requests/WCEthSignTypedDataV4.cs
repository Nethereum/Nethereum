using System.Collections.Generic;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Network.Models;

namespace Nethereum.WalletConnect.Requests
{
    [RpcMethod("eth_signTypedData_v4"), RpcRequestOptions(Clock.ONE_MINUTE, 99997)]
    public class WCEthSignTypedDataV4 : List<string>
    {
        public WCEthSignTypedDataV4(string account, string hexUtf8) : base(new string[] { account, hexUtf8 })
        {
        }

        public WCEthSignTypedDataV4()
        {
        }
    }

}
