using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;

namespace Nethereum.Accounts.AccountMessageSigning
{
    public class AccountSigningExternalService : IAccountSigningService
    {

        public IEthSignTypedDataV4 SignTypedDataV4 { get; }

        public IEthPersonalSign PersonalSign { get; }

        public AccountSigningExternalService(IEthExternalSigner ethExternalSigner)
        {
            SignTypedDataV4 = new EthSignTypedDataV4External(ethExternalSigner);
            PersonalSign = new EthPersonalExternalSign(ethExternalSigner);
        }
    }
}
