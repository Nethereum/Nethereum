using Nethereum.Accounts.AccountAbstraction;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.RPC.AccountSigning;

using Nethereum.Documentation;
namespace Nethereum.AccountAbstraction
{
    [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "NethereumSmartAccount - a modular ERC-7579 account with its validator module")]
    public class NethereumSmartAccount : SmartAccount
    {
        public IErc7579ValidatorModule Validator { get; }

        public NethereumSmartAccount(
            string address,
            IAccountSigningService signingService,
            IErc7579ValidatorModule validator,
            bool isDeployed,
            byte[]? salt = null,
            byte[]? initData = null)
            : base(address, signingService, isDeployed, salt, initData)
        {
            Validator = validator;
        }
    }
}
