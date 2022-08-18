using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.AccountSigning
{
    public class AccountSigningService : RpcClientWrapper, IAccountSigningService
    {

        public IEthSignTypedDataV4 SignTypedDataV4 { get; }

        public IEthPersonalSign PersonalSign { get; }

        public AccountSigningService(IClient client) : base(client)
        {
            SignTypedDataV4 = new EthSignTypedDataV4(client);
            PersonalSign = new EthPersonalSign(client);
        }
    }
}