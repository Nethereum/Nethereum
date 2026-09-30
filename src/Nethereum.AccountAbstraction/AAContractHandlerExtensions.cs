using Nethereum.AccountAbstraction.Signing;
using Nethereum.Contracts;
using Nethereum.RPC;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction
{
    public static class AAContractHandlerExtensions
    {
        public static AAContractHandler ChangeContractHandlerToAA<T>(
            this T service,
            IAccount account,
            IErc7579ValidatorModule? validator,
            IAccountAbstractionBundlerService bundlerService,
            string entryPointAddress,
            FactoryConfig factory = null)
            where T : ContractWeb3ServiceBase
        {
            var handler = AAContractHandler.CreateFromExistingContractService(
                service,
                account,
                validator,
                bundlerService,
                entryPointAddress);

            if (factory != null)
                handler.WithFactory(factory);

            service.ContractHandler = handler;
            return handler;
        }

        public static AAContractHandler ChangeContractHandlerToAA<T>(
            this T service,
            string accountAddress,
            IAccountSigningService signingService,
            IErc7579ValidatorModule? validator,
            IAccountAbstractionBundlerService bundlerService,
            string entryPointAddress,
            FactoryConfig factory = null)
            where T : ContractWeb3ServiceBase
        {
            var handler = AAContractHandler.CreateFromExistingContractService(
                service,
                accountAddress,
                signingService,
                validator,
                bundlerService,
                entryPointAddress);

            if (factory != null)
                handler.WithFactory(factory);

            service.ContractHandler = handler;
            return handler;
        }

        public static AAContractHandler ChangeContractHandlerToAA<T>(
            this T service,
            string accountAddress,
            EthECKey signerKey,
            IAccountAbstractionBundlerService bundlerService,
            string entryPointAddress,
            FactoryConfig factory = null)
            where T : ContractWeb3ServiceBase
        {
            var handler = AAContractHandler.CreateFromExistingContractService(
                service,
                accountAddress,
                signerKey,
                bundlerService,
                entryPointAddress);

            if (factory != null)
                handler.WithFactory(factory);

            service.ContractHandler = handler;
            return handler;
        }

        public static AAContractHandler SwitchToAccountAbstraction<T>(
            this T service,
            string accountAddress,
            EthECKey signerKey,
            IAccountAbstractionBundlerService bundlerService,
            string entryPointAddress,
            FactoryConfig factory = null)
            where T : IContractHandlerService
        {
            var handler = AAContractHandler.CreateFromContractHandler(
                service.ContractHandler,
                accountAddress,
                signerKey,
                bundlerService,
                entryPointAddress);

            if (factory != null)
                handler.WithFactory(factory);

            service.ContractHandler = handler;
            return handler;
        }
    }
}
