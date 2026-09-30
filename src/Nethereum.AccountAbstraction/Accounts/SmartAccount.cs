using Nethereum.Accounts.AccountAbstraction;
using Nethereum.RPC.AccountSigning;

using Nethereum.Documentation;
namespace Nethereum.AccountAbstraction
{
    [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "SmartAccount - any ERC-4337 account, deployed or counterfactual")]
    public class SmartAccount : AccountAbstractionAccount
    {
        public bool IsDeployed { get; }
        public byte[]? Salt { get; }
        public byte[]? InitData { get; }

        public SmartAccount(
            string address,
            IAccountSigningService signingService,
            bool isDeployed,
            byte[]? salt = null,
            byte[]? initData = null)
            : base(address, signingService)
        {
            IsDeployed = isDeployed;
            Salt = salt;
            InitData = initData;
        }
    }
}
