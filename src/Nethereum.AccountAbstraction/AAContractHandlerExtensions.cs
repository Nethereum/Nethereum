using Nethereum.Contracts;
using Nethereum.RPC;
using Nethereum.Signer;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction
{
    public static class AAContractHandlerExtensions
    {
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

        public static AAContractHandler ChangeContractHandlerToAA<T>(
            this T service,
            string accountAddress,
            EthECKey signerKey,
            string bundlerRpcUrl,
            string entryPointAddress,
            FactoryConfig factory = null)
            where T : ContractWeb3ServiceBase
        {
            var handler = AAContractHandler.CreateFromExistingContractService(
                service,
                accountAddress,
                signerKey,
                bundlerRpcUrl,
                entryPointAddress);

            if (factory != null)
                handler.WithFactory(factory);

            service.ContractHandler = handler;
            return handler;
        }

        public static AAContractHandler ChangeContractHandlerToAA<T>(
            this T service,
            string accountAddress,
            string privateKey,
            IAccountAbstractionBundlerService bundlerService,
            string entryPointAddress,
            FactoryConfig factory = null)
            where T : ContractWeb3ServiceBase
        {
            return service.ChangeContractHandlerToAA(
                accountAddress,
                new EthECKey(privateKey),
                bundlerService,
                entryPointAddress,
                factory);
        }

        public static AAContractHandler ChangeContractHandlerToAA<T>(
            this T service,
            string accountAddress,
            string privateKey,
            string bundlerRpcUrl,
            string entryPointAddress,
            FactoryConfig factory = null)
            where T : ContractWeb3ServiceBase
        {
            return service.ChangeContractHandlerToAA(
                accountAddress,
                new EthECKey(privateKey),
                bundlerRpcUrl,
                entryPointAddress,
                factory);
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

        public static AAContractHandler SwitchToAccountAbstraction<T>(
            this T service,
            string accountAddress,
            string privateKey,
            IAccountAbstractionBundlerService bundlerService,
            string entryPointAddress,
            FactoryConfig factory = null)
            where T : IContractHandlerService
        {
            return service.SwitchToAccountAbstraction(
                accountAddress,
                new EthECKey(privateKey),
                bundlerService,
                entryPointAddress,
                factory);
        }
    }
}
