using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Accounts;
using Nethereum.RPC;

namespace Nethereum.Parity
{
    public class AccountsApiService : RpcClientWrapper
    {
        public AccountsApiService(IClient client) : base(client)
        {
            AccountsInfo = new ParityAccountsInfo(client);
            DefaultAccount = new ParityDefaultAccount(client);
            GenerateSecretPhrase = new ParityGenerateSecretPhrase(client);
            HardwareAccountsInfo = new ParityHardwareAccountsInfo(client);
        }

        public ParityAccountsInfo AccountsInfo { get; private set; }
        public ParityDefaultAccount DefaultAccount { get; private set; }
        public ParityGenerateSecretPhrase GenerateSecretPhrase { get; private set; }
        public ParityHardwareAccountsInfo HardwareAccountsInfo { get; private set; }
    }
}