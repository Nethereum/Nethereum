using Nethereum.Parity.RPC.Accounts;

namespace Nethereum.Parity
{
    public interface IAccountsApiService
    {
        IParityAccountsInfo AccountsInfo { get; }
        IParityDefaultAccount DefaultAccount { get; }
        IParityGenerateSecretPhrase GenerateSecretPhrase { get; }
        IParityHardwareAccountsInfo HardwareAccountsInfo { get; }
    }
}