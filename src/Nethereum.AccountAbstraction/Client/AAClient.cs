using System.Security.Cryptography;
using System.Threading.Tasks;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.Factory;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Contracts;
using Nethereum.RPC;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Client
{
    public class AAClient : IAAClient
    {
        private readonly IWeb3 _web3;
        private readonly AADeploymentAddresses _deploymentAddresses;
        private readonly IAccountAbstractionBundlerService _bundler;
        private readonly NethereumAccountFactoryService _factoryService;

        public AAClient(IWeb3 web3, AADeploymentAddresses deploymentAddresses, IAccountAbstractionBundlerService bundler)
        {
            _web3 = web3;
            _deploymentAddresses = deploymentAddresses;
            _bundler = bundler;
            _factoryService = new NethereumAccountFactoryService(web3, deploymentAddresses.NethereumAccountFactoryAddress);
        }

        public Task<NethereumSmartAccount> CreateAccountAsync(EthECKey owner, byte[]? salt = null)
        {
            var signingService = new AccountSigningOfflineService(owner);
            var validator = new EcdsaValidatorModule(_deploymentAddresses.EcdsaValidatorAddress);
            var initData = AccountInitDataBuilder.BuildEcdsa(_deploymentAddresses.EcdsaValidatorAddress, owner.GetPublicAddress());

            return CreateAccountAsync(signingService, validator, initData, salt);
        }

        public async Task<NethereumSmartAccount> CreateAccountAsync(IAccountSigningService signingService, IErc7579ValidatorModule validator, byte[] initData, byte[]? salt = null)
        {
            salt ??= GenerateSalt();
            var address = await _factoryService.GetAddressQueryAsync(salt, initData).ConfigureAwait(false);

            var code = await _web3.Eth.GetCode.SendRequestAsync(address).ConfigureAwait(false);
            var isDeployed = !string.IsNullOrEmpty(code) && code != "0x";

            return new NethereumSmartAccount(address, signingService, validator, isDeployed, salt, initData);
        }

        public NethereumSmartAccount GetAccount(string address, IAccountSigningService signingService, IErc7579ValidatorModule validator)
        {
            return new NethereumSmartAccount(address, signingService, validator, isDeployed: true);
        }

        public SmartAccount GetAccount(string address, IAccountSigningService signingService)
        {
            return new SmartAccount(address, signingService, isDeployed: true);
        }

        public AAContractHandler Configure<TService>(TService service, NethereumSmartAccount account)
            where TService : ContractWeb3ServiceBase
        {
            var handler = service.ChangeContractHandlerToAA(account, account.Validator, _bundler, _deploymentAddresses.EntryPointAddress)
                .WithErc7579Execution();

            if (!account.IsDeployed && account.Salt != null && account.InitData != null)
                handler.WithFactory(new NethereumAccountInitCodeBuilder(
                    _deploymentAddresses.NethereumAccountFactoryAddress, account.Salt, account.InitData));

            return handler;
        }

        public AAContractHandler Configure<TService>(TService service, SmartAccount account)
            where TService : ContractWeb3ServiceBase
        {
            if (account is NethereumSmartAccount modularAccount)
                return Configure(service, modularAccount);

            return service.ChangeContractHandlerToAA(account, validator: null, _bundler, _deploymentAddresses.EntryPointAddress);
        }

        public AAContractHandler ConfigureEip7702<TService>(
            TService service, NethereumSmartAccount account, EthECKey ownerKey, string accountImplementationAddress)
            where TService : ContractWeb3ServiceBase
        {
            var factoryData = account.InitData is { Length: > 0 }
                ? new InitializeAccountFunction { InitData = account.InitData }.GetCallData()
                : null;

            var handler = new AAContractHandler(
                    service.ContractAddress,
                    account.Address,
                    ownerKey,
                    account.Validator,
                    _bundler,
                    _deploymentAddresses.EntryPointAddress,
                    _web3)
                .WithEip7702Delegation(accountImplementationAddress, factoryData)
                .WithErc7579Execution();

            service.ContractHandler = handler;
            return handler;
        }

        private static byte[] GenerateSalt()
        {
            var salt = new byte[32];
            RandomNumberGenerator.Fill(salt);
            return salt;
        }
    }
}
