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

        public ParityAccountsInfo AccountsInfo { get; }
        public ParityDefaultAccount DefaultAccount { get; }
        public ParityGenerateSecretPhrase GenerateSecretPhrase { get; }
        public ParityHardwareAccountsInfo HardwareAccountsInfo { get; }
    }
}