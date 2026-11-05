using Nethereum.Parity.RPC.Accounts;

namespace Nethereum.Parity
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ClientExtensions, "parity-client", "IAccountsApiService")]
    public interface IAccountsApiService
    {
        IParityAccountsInfo AccountsInfo { get; }
        IParityDefaultAccount DefaultAccount { get; }
        IParityGenerateSecretPhrase GenerateSecretPhrase { get; }
        IParityHardwareAccountsInfo HardwareAccountsInfo { get; }
    }
}