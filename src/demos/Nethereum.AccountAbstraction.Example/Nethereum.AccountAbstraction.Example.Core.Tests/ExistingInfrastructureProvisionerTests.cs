using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.AccountAbstraction.Bundler.RpcServer.Configuration;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.Example.Hosting;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain.Accounts;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.Signer;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    public abstract class ExistingInfrastructureHttpFixture : IAsyncLifetime
    {
        private WebApplication? _nodeApp;
        private WebApplication? _bundlerApp;

        public string NodeUrl { get; private set; } = null!;
        public string BundlerUrl { get; private set; } = null!;
        public string FunderPrivateKey { get; private set; } = null!;
        public BigInteger ChainId { get; private set; }
        public string PreDeployedEntryPointAddress { get; private set; } = null!;

        protected abstract string Hardfork { get; }

        public async Task InitializeAsync()
        {
            var chainConfig = new DevChainServerConfig { ChainId = 31337, Storage = "memory", Hardfork = Hardfork };

            var nodeBuilder = WebApplication.CreateBuilder();
            nodeBuilder.AddDevChainServer(chainConfig);
            nodeBuilder.Services.AddLogging(l => l.SetMinimumLevel(LogLevel.Warning));
            _nodeApp = nodeBuilder.Build();
            await _nodeApp.MapDevChainEndpointsAsync();
            _nodeApp.Urls.Add("http://127.0.0.1:0");
            await _nodeApp.StartAsync();
            NodeUrl = _nodeApp.Urls.First();

            var accountManager = _nodeApp.Services.GetRequiredService<DevAccountManager>();
            ChainId = chainConfig.ChainId;

            var funderDevAccount = accountManager.Accounts[0];
            FunderPrivateKey = funderDevAccount.GetPrivateKeyHex();

            var funderWeb3 = new Nethereum.Web3.Web3(funderDevAccount.Account, NodeUrl);
            var entryPoint = await EntryPointService.DeployContractAndGetServiceAsync(funderWeb3, new EntryPointDeployment());
            PreDeployedEntryPointAddress = entryPoint.ContractAddress;

            var bundlerDevAccount = accountManager.Accounts[1];
            var bundlerConfig = new BundlerRpcServerConfig
            {
                RpcUrl = NodeUrl,
                ChainId = ChainId,
                SupportedEntryPoints = new[] { entryPoint.ContractAddress },
                BeneficiaryAddress = bundlerDevAccount.Address,
                PrivateKey = bundlerDevAccount.GetPrivateKeyHex(),
                UnsafeMode = false,
                SimulateValidation = true,
                StrictValidation = true,
                AutoBundleIntervalMs = 200
            };

            var bundlerBuilder = WebApplication.CreateBuilder();
            bundlerBuilder.Services.AddBundlerRpcServer(bundlerConfig);
            bundlerBuilder.Services.AddLogging(l => l.SetMinimumLevel(LogLevel.Warning));
            _bundlerApp = bundlerBuilder.Build();

            var dispatcher = _bundlerApp.Services.GetRequiredService<RpcDispatcher>();
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            _bundlerApp.MapPost("/", async (HttpContext httpContext) =>
            {
                using var reader = new StreamReader(httpContext.Request.Body);
                var json = await reader.ReadToEndAsync();
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(json, jsonOptions);
                if (request is null)
                {
                    httpContext.Response.StatusCode = 400;
                    return;
                }

                var response = await dispatcher.DispatchAsync(request.ToRpcRequestMessage());
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(response.ToJsonRpcResponse(), jsonOptions));
            });
            _bundlerApp.Urls.Add("http://127.0.0.1:0");
            await _bundlerApp.StartAsync();
            BundlerUrl = _bundlerApp.Urls.First();
        }

        public async Task DisposeAsync()
        {
            if (_bundlerApp is not null)
            {
                await _bundlerApp.StopAsync();
                await _bundlerApp.DisposeAsync();
            }
            if (_nodeApp is not null)
            {
                await _nodeApp.StopAsync();
                await _nodeApp.DisposeAsync();
            }
        }
    }

    public sealed class OsakaExistingInfrastructureFixture : ExistingInfrastructureHttpFixture
    {
        protected override string Hardfork => "osaka";
    }

    public sealed class AmsterdamExistingInfrastructureFixture : ExistingInfrastructureHttpFixture
    {
        protected override string Hardfork => "amsterdam";
    }

    public class ExistingInfrastructureProvisionerTestsAtOsaka
        : ExistingInfrastructureProvisionerTests<OsakaExistingInfrastructureFixture>
    {
        public ExistingInfrastructureProvisionerTestsAtOsaka(OsakaExistingInfrastructureFixture fixture) : base(fixture) { }
    }

    public class ExistingInfrastructureProvisionerTestsAtAmsterdam
        : ExistingInfrastructureProvisionerTests<AmsterdamExistingInfrastructureFixture>
    {
        public ExistingInfrastructureProvisionerTestsAtAmsterdam(AmsterdamExistingInfrastructureFixture fixture) : base(fixture) { }
    }

    public abstract class ExistingInfrastructureProvisionerTests<TFixture> : IClassFixture<TFixture>
        where TFixture : ExistingInfrastructureHttpFixture
    {
        private readonly ExistingInfrastructureHttpFixture _fixture;

        protected ExistingInfrastructureProvisionerTests(TFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        [Trait("UseCase", "InfrastructureSetup")]
        public async Task Given_a_providers_already_deployed_standard_modules_When_the_use_existing_provisioner_connects_Then_the_client_interoperates_through_the_supplied_modules_and_gasless_degrades_gracefully()
        {
            var providerWeb3 = new Nethereum.Web3.Web3(new Web3Account(_fixture.FunderPrivateKey, _fixture.ChainId), _fixture.NodeUrl);
            var providerBundler = new AccountAbstractionBundlerService(new RpcClient(new System.Uri(_fixture.BundlerUrl)));
            var providerLog = new List<string>();
            var standard = await HostBootstrap.DeployStandardStackAsync(providerWeb3, providerBundler, line => providerLog.Add(line));

            var supplied = new ExistingModuleAddresses(
                EntryPointAddress: standard.Addresses.EntryPointAddress,
                NethereumAccountFactoryAddress: standard.Addresses.NethereumAccountFactoryAddress,
                EcdsaValidatorAddress: standard.Addresses.EcdsaValidatorAddress,
                WebAuthnValidatorAddress: standard.WebAuthnConfig.ValidatorAddress,
                OwnableExecutorAddress: standard.OwnableExecutor.ContractAddress,
                SocialRecoveryAddress: standard.SocialRecoveryConfig.SocialRecoveryAddress,
                SmartSessionAddress: standard.PoliciesConfig.SmartSessionAddress,
                SudoPolicyAddress: standard.PoliciesConfig.SudoPolicyAddress,
                UniActionPolicyAddress: standard.PoliciesConfig.UniActionPolicyAddress,
                EcdsaSessionValidatorAddress: standard.PoliciesConfig.SessionValidatorAddress,
                AccountImplementationAddress: standard.NethereumAccountImplementationAddress);

            var provisioner = new ExistingInfrastructureProvisioner();
            var request = new ExistingInfrastructureRequest(_fixture.NodeUrl, _fixture.BundlerUrl, _fixture.FunderPrivateKey, supplied);
            var log = new List<string>();

            var provisioned = await provisioner.ProvisionAsync(request, line => log.Add(line));

            Assert.Null(provisioned.Resource);
            Assert.Null(provisioned.Stack.Paymaster);
            Assert.Equal(supplied.EntryPointAddress, provisioned.Stack.Addresses.EntryPointAddress, ignoreCase: true);
            Assert.Equal(supplied.NethereumAccountFactoryAddress, provisioned.Stack.Addresses.NethereumAccountFactoryAddress, ignoreCase: true);
            Assert.Equal(supplied.EcdsaValidatorAddress, provisioned.Stack.Addresses.EcdsaValidatorAddress, ignoreCase: true);
            Assert.Equal(supplied.WebAuthnValidatorAddress, provisioned.Stack.WebAuthnConfig.ValidatorAddress, ignoreCase: true);
            Assert.Equal(supplied.SmartSessionAddress, provisioned.Stack.PoliciesConfig.SmartSessionAddress, ignoreCase: true);

            Assert.False(string.IsNullOrEmpty(provisioned.Stack.TestCounter.ContractAddress));
            Assert.False(string.IsNullOrEmpty(provisioned.Stack.BookingRegistry.ContractAddress));

            var session = new SessionState();
            session.PublishInfra(provisioned.Stack, provisioned.Web3, provisioned.Bundler, provisioned.Faucet);

            var setupViewModel = new SetupViewModel(session);
            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);
            Assert.Null(setupViewModel.ErrorMessage);
            Assert.NotNull(session.Account);

            await session.Faucet!.FundAsync(session.Account!.Address);

            var interactionViewModel = new InteractionViewModel(session);
            await interactionViewModel.SendCountCommand.ExecuteAsync(null);
            Assert.Null(interactionViewModel.ErrorMessage);
            Assert.NotNull(interactionViewModel.LastReceipt);
            Assert.True(interactionViewModel.LastReceipt!.UserOpSuccess, interactionViewModel.LastReceipt.FailureDiagnostic);
            Assert.Equal(BigInteger.One, interactionViewModel.Count);

            var gaslessViewModel = new GaslessViewModel(session);
            Assert.False(gaslessViewModel.HasPaymaster);
            await gaslessViewModel.SendGaslessCommand.ExecuteAsync(null);
            Assert.False(gaslessViewModel.IsBusy);
            Assert.Null(gaslessViewModel.LastReceipt);
            Assert.False(string.IsNullOrEmpty(gaslessViewModel.ErrorMessage));
            Assert.Contains("paymaster", gaslessViewModel.ErrorMessage, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [Trait("UseCase", "InfrastructureSetup")]
        public async Task Given_a_supplied_module_address_with_no_on_chain_code_When_BuildFromExistingAsync_runs_Then_it_throws_naming_the_module()
        {
            var funderWeb3 = new Nethereum.Web3.Web3(new Web3Account(_fixture.FunderPrivateKey, _fixture.ChainId), _fixture.NodeUrl);
            var bundler = new AccountAbstractionBundlerService(new RpcClient(new System.Uri(_fixture.BundlerUrl)));

            var noCodeAddress = EthECKey.GenerateKey().GetPublicAddress();
            var supplied = new ExistingModuleAddresses(
                EntryPointAddress: noCodeAddress,
                NethereumAccountFactoryAddress: noCodeAddress,
                EcdsaValidatorAddress: noCodeAddress,
                WebAuthnValidatorAddress: noCodeAddress,
                OwnableExecutorAddress: noCodeAddress,
                SocialRecoveryAddress: noCodeAddress,
                SmartSessionAddress: noCodeAddress,
                SudoPolicyAddress: noCodeAddress,
                UniActionPolicyAddress: noCodeAddress,
                EcdsaSessionValidatorAddress: noCodeAddress,
                AccountImplementationAddress: noCodeAddress);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => HostBootstrap.BuildFromExistingAsync(funderWeb3, bundler, supplied));

            Assert.Contains("EntryPoint", ex.Message);
            Assert.Contains(noCodeAddress, ex.Message);
            Assert.Contains("has no code on this chain", ex.Message);
        }
    }
}
