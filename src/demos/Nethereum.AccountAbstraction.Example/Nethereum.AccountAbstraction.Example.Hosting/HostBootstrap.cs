using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.WebAuthn.Contracts.Modules.Rhinestone.WebAuthnValidator;
using Nethereum.AccountAbstraction.WebAuthn.Contracts.Modules.Rhinestone.WebAuthnValidator.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableExecutor;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Example.Contracts.SmartSessionRegistryStub;
using Nethereum.AccountAbstraction.Example.Contracts.SmartSessionRegistryStub.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.Example.Contracts.BookingRegistry;
using Nethereum.AccountAbstraction.Example.Contracts.BookingRegistry.ContractDefinition;
using Nethereum.AccountAbstraction.Example.Contracts.TestCounter;
using Nethereum.AccountAbstraction.Example.Contracts.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Deployment;
using Nethereum.AccountAbstraction.Example.Core;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC;
using Nethereum.RPC.Extensions.DevTools.Hardhat;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.Example.Hosting
{
    public sealed record EmbeddedInfra(
        InProcessBundlerHost Bootstrap,
        IWeb3 OperatorWeb3,
        IAccountAbstractionBundlerService Bundler,
        IDevChainFaucet Faucet);

    public sealed record StandardStack(
        AADeploymentAddresses Addresses,
        IAAClient Client,
        WebAuthnAccountConfig WebAuthnConfig,
        Eip7702AccountConfig Eip7702Config,
        SocialRecoveryAccountConfig SocialRecoveryConfig,
        PoliciesAccountConfig PoliciesConfig,
        OwnableExecutorService OwnableExecutor,
        SmartSessionService SmartSession,
        string NethereumAccountImplementationAddress,
        bool EntryPointWasFreshlyDeployed);

    public sealed record DemoContracts(TestCounterService TestCounter, BookingRegistryService BookingRegistry);

    public sealed class HostBootstrap
    {
        public const int ChainId = 31337;

        private const string SmartSessionRegistryAddress = "0x000000000069E2a187AEFFb852bF3cCdC95151B2";

        private const string OwnerPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string BundlerPrivateKey = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";

        public InProcessBundlerHost Bootstrap { get; }
        public IAccountAbstractionBundlerService Bundler { get; }
        public IDevChainFaucet Faucet { get; }
        public DeployedStack Stack { get; }

        public AADeploymentAddresses DeploymentAddresses => Stack.Addresses;
        public string WebAuthnValidatorAddress => Stack.WebAuthnConfig.ValidatorAddress;
        public OwnableExecutorService OwnableExecutor => Stack.OwnableExecutor;
        public string OwnableExecutorAddress => Stack.OwnableExecutor.ContractAddress;
        public string SocialRecoveryAddress => Stack.SocialRecoveryConfig.SocialRecoveryAddress;
        public SmartSessionService SmartSession => Stack.SmartSession;
        public PoliciesAccountConfig PoliciesConfig => Stack.PoliciesConfig;
        public string NethereumAccountImplementationAddress => Stack.NethereumAccountImplementationAddress;
        public TestCounterService TestCounter => Stack.TestCounter;
        public BookingRegistryService BookingRegistry => Stack.BookingRegistry;
        public PaymasterConfig PaymasterConfig => Stack.Paymaster!;

        private HostBootstrap(EmbeddedInfra infra, DeployedStack stack)
        {
            Bootstrap = infra.Bootstrap;
            Bundler = infra.Bundler;
            Faucet = infra.Faucet;
            Stack = stack;
        }

        public Task FundAsync(string address, decimal ether = 10m) => Faucet.FundAsync(address, ether);

        public void PublishInto(SessionState session) =>
            session.PublishInfra(Stack, Bootstrap.OperatorWeb3, Bundler, Faucet);

        public static async Task<EmbeddedInfra> StartInfraAsync(Action<string>? log = null)
        {
            log ??= _ => { };
            var operatorAccount = new Web3Account(OwnerPrivateKey, ChainId);
            var bundlerAccount = new Web3Account(BundlerPrivateKey, ChainId);

            log("Starting in-process DevChain...");
            var bootstrap = await InProcessBundlerHost.StartAsync(
                operatorAccount,
                ChainId,
                new[] { operatorAccount.Address, bundlerAccount.Address },
                Web3.Web3.Convert.ToWei(10000));

            log("Deploying the EntryPoint...");
            var entryPoint = await EntryPointService.DeployContractAndGetServiceAsync(
                bootstrap.OperatorWeb3, new EntryPointDeployment());
            log($"EntryPoint deployed at {entryPoint.ContractAddress}");

            log("Starting in-process bundler (ERC-4337 validation ON)...");
            var bundler = bootstrap.StartBundler(entryPoint.ContractAddress, bundlerAccount);

            var faucet = new DevChainFaucet(bootstrap);
            return new EmbeddedInfra(bootstrap, bootstrap.OperatorWeb3, bundler, faucet);
        }

        public static async Task<DeployedStack> DeployStackAsync(
            IWeb3 operatorWeb3, IAccountAbstractionBundlerService bundler, Action<string>? log = null)
        {
            log ??= _ => { };
            var operatorAddress = operatorWeb3.TransactionManager.Account.Address;

            var standard = await DeployStandardStackAsync(operatorWeb3, bundler, log).ConfigureAwait(false);
            var demo = await DeployDemoContractsAsync(operatorWeb3, log).ConfigureAwait(false);

            log("Deploying and funding the sponsoring VerifyingPaymaster...");
            var paymasterConfig = await DeploySponsoringPaymasterAsync(
                operatorWeb3, standard.Addresses.EntryPointAddress, operatorAddress).ConfigureAwait(false);

            return new DeployedStack(
                standard.Addresses,
                standard.Client,
                demo.TestCounter,
                demo.BookingRegistry,
                paymasterConfig,
                standard.WebAuthnConfig,
                standard.Eip7702Config,
                standard.SocialRecoveryConfig,
                standard.PoliciesConfig,
                standard.OwnableExecutor,
                standard.SmartSession,
                standard.NethereumAccountImplementationAddress,
                standard.EntryPointWasFreshlyDeployed);
        }

        public static async Task<StandardStack> DeployStandardStackAsync(
            IWeb3 operatorWeb3, IAccountAbstractionBundlerService bundler, Action<string>? log = null)
        {
            log ??= _ => { };

            var supportedEntryPoints = await bundler.SupportedEntryPoints.SendRequestAsync();
            if (supportedEntryPoints is null || supportedEntryPoints.Length == 0)
                throw new InvalidOperationException(
                    "The bundler reports no supported EntryPoints (eth_supportedEntryPoints) - it must be configured against a deployed EntryPoint before the stack can be deployed.");
            var entryPointAddress = supportedEntryPoints[0];

            var entryPointCode = await operatorWeb3.Eth.GetCode.SendRequestAsync(entryPointAddress);
            bool entryPointWasFreshlyDeployed;
            if (string.IsNullOrEmpty(entryPointCode) || entryPointCode == "0x")
            {
                log($"Bundler-supported EntryPoint {entryPointAddress} has no code on this chain - deploying a fallback EntryPoint through the funder account. Point the bundler at the address below if it only accepts {entryPointAddress}.");
                var fallbackEntryPoint = await EntryPointService.DeployContractAndGetServiceAsync(
                    operatorWeb3, new EntryPointDeployment());
                entryPointAddress = fallbackEntryPoint.ContractAddress;
                entryPointWasFreshlyDeployed = true;
                log($"Fallback EntryPoint deployed at {entryPointAddress}");
            }
            else
            {
                entryPointWasFreshlyDeployed = false;
                log($"Using EntryPoint {entryPointAddress} (from the bundler's own eth_supportedEntryPoints, already deployed on-chain)");
            }

            log("Deploying the ERC-7579 module contracts (ECDSAValidator / SmartSession stack / SocialRecovery / OwnableExecutor)...");
            var modules = await new AAModuleDeployer(operatorWeb3).DeployAsync(
                new AAModuleDeploymentOptions { OwnableExecutor = true });
            var smartSession = new SmartSessionService(operatorWeb3, modules.SmartSession);
            var ownableExecutor = new OwnableExecutorService(operatorWeb3, modules.OwnableExecutor);

            var webAuthnValidator = await WebAuthnValidatorService.DeployContractAndGetServiceAsync(
                operatorWeb3, new WebAuthnValidatorDeployment());
            var factory = await NethereumAccountFactoryService.DeployContractAndGetServiceAsync(
                operatorWeb3,
                new NethereumAccountFactoryDeployment { EntryPoint = entryPointAddress });
            var accountImplementationAddress = await factory.AccountImplementationQueryAsync();
            log($"WebAuthnValidator deployed at {webAuthnValidator.ContractAddress}");
            log($"SmartSession deployed at {modules.SmartSession}");
            log($"SocialRecovery deployed at {modules.SocialRecovery}");
            log($"NethereumAccount implementation deployed at {accountImplementationAddress}");

            var policiesConfig = new PoliciesAccountConfig(
                modules.SmartSession, modules.SudoPolicy, modules.UniActionPolicy, modules.EcdsaSessionValidator);

            await TryEtchSmartSessionRegistryStubAsync(operatorWeb3, log);

            var addresses = new AADeploymentAddresses(
                entryPointAddress,
                factory.ContractAddress,
                modules.EcdsaValidator,
                VerifyingPaymasterAddress: string.Empty);

            log("Building the IAAClient on-ramp...");
            var client = BuildClient(operatorWeb3, addresses, bundler);

            return new StandardStack(
                addresses,
                client,
                new WebAuthnAccountConfig(webAuthnValidator.ContractAddress, RelyingPartyId),
                new Eip7702AccountConfig(accountImplementationAddress, modules.EcdsaValidator),
                new SocialRecoveryAccountConfig(modules.SocialRecovery, modules.EcdsaValidator),
                policiesConfig,
                ownableExecutor,
                smartSession,
                accountImplementationAddress,
                entryPointWasFreshlyDeployed);
        }

        public static async Task<DemoContracts> DeployDemoContractsAsync(IWeb3 operatorWeb3, Action<string>? log = null)
        {
            log ??= _ => { };
            log("Deploying demo contracts (TestCounter, BookingRegistry)...");
            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                operatorWeb3, new TestCounterDeployment());
            var bookingRegistry = await BookingRegistryService.DeployContractAndGetServiceAsync(
                operatorWeb3, new BookingRegistryDeployment());
            return new DemoContracts(testCounter, bookingRegistry);
        }

        public static async Task<DeployedStack> BuildFromExistingAsync(
            IWeb3 operatorWeb3, IAccountAbstractionBundlerService bundler, ExistingModuleAddresses supplied, Action<string>? log = null)
        {
            if (supplied is null) throw new ArgumentNullException(nameof(supplied));
            log ??= _ => { };

            log("Verifying the supplied standard module addresses have code on-chain...");
            await RequireCodeAsync(operatorWeb3, supplied.EntryPointAddress, "EntryPoint", log).ConfigureAwait(false);
            await RequireCodeAsync(operatorWeb3, supplied.NethereumAccountFactoryAddress, "NethereumAccountFactory", log).ConfigureAwait(false);
            await RequireCodeAsync(operatorWeb3, supplied.EcdsaValidatorAddress, "ECDSAValidator", log).ConfigureAwait(false);
            await RequireCodeAsync(operatorWeb3, supplied.WebAuthnValidatorAddress, "WebAuthnValidator", log).ConfigureAwait(false);
            await RequireCodeAsync(operatorWeb3, supplied.OwnableExecutorAddress, "OwnableExecutor", log).ConfigureAwait(false);
            await RequireCodeAsync(operatorWeb3, supplied.SocialRecoveryAddress, "SocialRecovery", log).ConfigureAwait(false);
            await RequireCodeAsync(operatorWeb3, supplied.SmartSessionAddress, "SmartSession", log).ConfigureAwait(false);
            await RequireCodeAsync(operatorWeb3, supplied.SudoPolicyAddress, "SudoPolicy", log).ConfigureAwait(false);
            await RequireCodeAsync(operatorWeb3, supplied.UniActionPolicyAddress, "UniActionPolicy", log).ConfigureAwait(false);
            await RequireCodeAsync(operatorWeb3, supplied.EcdsaSessionValidatorAddress, "ECDSASessionValidator", log).ConfigureAwait(false);
            await RequireCodeAsync(operatorWeb3, supplied.AccountImplementationAddress, "NethereumAccount implementation", log).ConfigureAwait(false);
            log("All supplied module addresses have code on-chain.");

            var addresses = new AADeploymentAddresses(
                supplied.EntryPointAddress,
                supplied.NethereumAccountFactoryAddress,
                supplied.EcdsaValidatorAddress,
                VerifyingPaymasterAddress: string.Empty);

            log("Building the IAAClient on-ramp against the supplied standard modules...");
            var client = BuildClient(operatorWeb3, addresses, bundler);

            var demo = await DeployDemoContractsAsync(operatorWeb3, log).ConfigureAwait(false);

            return new DeployedStack(
                addresses,
                client,
                demo.TestCounter,
                demo.BookingRegistry,
                Paymaster: null,
                new WebAuthnAccountConfig(supplied.WebAuthnValidatorAddress, RelyingPartyId),
                new Eip7702AccountConfig(supplied.AccountImplementationAddress, supplied.EcdsaValidatorAddress),
                new SocialRecoveryAccountConfig(supplied.SocialRecoveryAddress, supplied.EcdsaValidatorAddress),
                new PoliciesAccountConfig(supplied.SmartSessionAddress, supplied.SudoPolicyAddress, supplied.UniActionPolicyAddress, supplied.EcdsaSessionValidatorAddress),
                new OwnableExecutorService(operatorWeb3, supplied.OwnableExecutorAddress),
                new SmartSessionService(operatorWeb3, supplied.SmartSessionAddress),
                supplied.AccountImplementationAddress,
                EntryPointWasFreshlyDeployed: false);
        }

        private static async Task RequireCodeAsync(IWeb3 web3, string address, string moduleName, Action<string> log)
        {
            var code = await web3.Eth.GetCode.SendRequestAsync(address).ConfigureAwait(false);
            if (string.IsNullOrEmpty(code) || code == "0x")
                throw new InvalidOperationException(
                    $"The supplied {moduleName} address {address} has no code on this chain - check the address and that you are connected to the right node.");
        }

        private static IAAClient BuildClient(IWeb3 operatorWeb3, AADeploymentAddresses addresses, IAccountAbstractionBundlerService bundler)
        {
            var services = new ServiceCollection();
            services.AddNethereumAccountAbstraction(o => o
                .UseWeb3(operatorWeb3)
                .UseDeploymentAddresses(addresses)
                .UseBundler(bundler));
            return services.BuildServiceProvider().GetRequiredService<IAAClient>();
        }

        private const string RelyingPartyId = "localhost";

        private static async Task TryEtchSmartSessionRegistryStubAsync(IWeb3 operatorWeb3, Action<string> log)
        {
            try
            {
                var registryStub = await SmartSessionRegistryStubService.DeployContractAndGetServiceAsync(
                    operatorWeb3, new SmartSessionRegistryStubDeployment());
                var registryStubCode = await operatorWeb3.Eth.GetCode.SendRequestAsync(registryStub.ContractAddress);
                await new HardhatSetCode(operatorWeb3.Client).SendRequestAsync(SmartSessionRegistryAddress, registryStubCode);
                log($"SmartSession registry stub etched at {SmartSessionRegistryAddress}");
            }
            catch (Exception ex)
            {
                log($"SmartSession registry stub etch skipped (not a DevChain-compatible chain): {ex.Message}");
            }
        }

        public static async Task<PaymasterConfig> DeploySponsoringPaymasterAsync(
            IWeb3 operatorWeb3, string entryPointAddress, string operatorAddress, decimal depositEth = 5m)
        {
            var signerKey = EthECKey.GenerateKey();

            var paymaster = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                operatorWeb3,
                new VerifyingPaymasterDeployment
                {
                    EntryPoint = entryPointAddress,
                    Owner = operatorAddress,
                    Signer = signerKey.GetPublicAddress()
                });

            if (depositEth > 0)
            {
                await paymaster.DepositRequestAndWaitForReceiptAsync(
                    new DepositFunction { AmountToSend = Web3.Web3.Convert.ToWei(depositEth) });
            }

            return new PaymasterConfig(paymaster.ContractAddress, BuildSigningDataProvider(paymaster, signerKey));
        }

        private static Func<UserOperation, Task<byte[]>> BuildSigningDataProvider(
            VerifyingPaymasterService paymaster, EthECKey signerKey)
        {
            return async userOp =>
            {
                var packedOp = UserOperationBuilder.PackUserOperation(userOp);
                packedOp.InitCode ??= Array.Empty<byte>();
                packedOp.CallData ??= Array.Empty<byte>();
                packedOp.PaymasterAndData ??= Array.Empty<byte>();
                packedOp.Signature ??= Array.Empty<byte>();
                var validUntil = (ulong)DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
                const ulong validAfter = 0;

                var hash = await paymaster.GetHashQueryAsync(packedOp, validUntil, validAfter);
                var prefixedHash = new EthereumMessageSigner().HashPrefixedMessage(hash);
                var signature = signerKey.SignAndCalculateV(prefixedHash);
                var signatureBytes = EthECDSASignature.CreateStringSignature(signature).HexToByteArray();

                return ByteUtil.Merge(ToUint48BigEndian(validUntil), ToUint48BigEndian(validAfter), signatureBytes);
            };
        }

        private static byte[] ToUint48BigEndian(ulong value)
        {
            var bytes = new byte[6];
            for (var i = 5; i >= 0; i--)
            {
                bytes[i] = (byte)(value & 0xFF);
                value >>= 8;
            }
            return bytes;
        }

        public static async Task<HostBootstrap> StartAsync(Action<string>? log = null)
        {
            log ??= _ => { };
            var infra = await StartInfraAsync(log);
            var stack = await DeployStackAsync(infra.OperatorWeb3, infra.Bundler, log);
            return new HostBootstrap(infra, stack);
        }
    }
}
