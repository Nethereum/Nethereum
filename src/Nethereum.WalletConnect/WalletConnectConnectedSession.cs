using WalletConnectSharp.Sign.Models;

namespace Nethereum.WalletConnect
{
    public class WalletConnectConnectedSession
    {
        public string Address { get; set; }
        public string ChainId { get; set; }
        public SessionStruct Session { get; set; }
    }

}

