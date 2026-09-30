using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;
using Nethereum.Web3;

using Nethereum.Documentation;
namespace Nethereum.AccountAbstraction.Client
{
    public interface IAAClient
    {
        Task<NethereumSmartAccount> CreateAccountAsync(EthECKey owner, byte[]? salt = null);

        [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "IAAClient.CreateAccountAsync - the signer-agnostic account on-ramp")]
        Task<NethereumSmartAccount> CreateAccountAsync(IAccountSigningService signingService, IErc7579ValidatorModule validator, byte[] initData, byte[]? salt = null);

        NethereumSmartAccount GetAccount(string address, IAccountSigningService signingService, IErc7579ValidatorModule validator);

        SmartAccount GetAccount(string address, IAccountSigningService signingService);

        /// <summary>
        /// Routes <paramref name="service"/> through Account Abstraction for <paramref name="account"/>:
        /// wires the signer, the configured bundler and EntryPoint, the ERC-7579 execute encoding, and -
        /// for a not-yet-deployed account - the factory that deploys it on the first op. Today this is
        /// the whole send pipeline (bundler-estimate "Simulate" then send "Execute", verified by the
        /// returned <see cref="AATransactionReceipt"/>); EvaluatePolicy/CollectApprovals hook in ahead of
        /// Execute once the platform gains them and are not implemented here.
        /// </summary>
        AAContractHandler Configure<TService>(TService service, NethereumSmartAccount account)
            where TService : ContractWeb3ServiceBase;

        AAContractHandler Configure<TService>(TService service, SmartAccount account)
            where TService : ContractWeb3ServiceBase;

        [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "IAAClient.ConfigureEip7702 - route a service through an EIP-7702-delegated EOA")]
        AAContractHandler ConfigureEip7702<TService>(
            TService service, NethereumSmartAccount account, EthECKey ownerKey, string accountImplementationAddress)
            where TService : ContractWeb3ServiceBase;
    }
}
