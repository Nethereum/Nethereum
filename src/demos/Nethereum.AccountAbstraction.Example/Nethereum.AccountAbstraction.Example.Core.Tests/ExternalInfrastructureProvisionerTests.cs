using System;
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
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    public sealed class ExternalHttpFixture : IAsyncLifetime
    {
        private WebApplication? _nodeApp;
        private WebApplication? _bundlerApp;

        public string NodeUrl { get; private set; } = null!;
        public string BundlerUrl { get; private set; } = null!;
        public string FunderPrivateKey { get; private set; } = null!;
        public string FunderAddress { get; private set; } = null!;
        public BigInteger ChainId { get; private set; }
        public string PreDeployedEntryPointAddress { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            var chainConfig = new DevChainServerConfig { ChainId = 31337, Storage = "memory" };

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
            FunderAddress = funderDevAccount.Address;

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
                AutoBundleIntervalMs = 0
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

    public class ExternalInfrastructureProvisionerTests : IClassFixture<ExternalHttpFixture>
    {
        private readonly ExternalHttpFixture _fixture;

        public ExternalInfrastructureProvisionerTests(ExternalHttpFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        [Trait("UseCase", "InfrastructureSetup")]
        public async Task Given_a_real_external_node_and_bundler_When_the_provisioner_deploys_Then_the_stack_is_live_and_the_faucet_moves_real_balance()
        {
            var log = new List<string>();
            var provisioner = new ExternalInfrastructureProvisioner();
            var request = new ExternalInfrastructureRequest(_fixture.NodeUrl, _fixture.BundlerUrl, _fixture.FunderPrivateKey);

            var provisioned = await provisioner.ProvisionAsync(request, line => log.Add(line));

            Assert.Equal(_fixture.FunderAddress, provisioned.Web3.TransactionManager.Account.Address, ignoreCase: true);
            Assert.Null(provisioned.Resource);

            Assert.False(string.IsNullOrEmpty(provisioned.Stack.Addresses.EntryPointAddress));
            Assert.Equal(_fixture.PreDeployedEntryPointAddress, provisioned.Stack.Addresses.EntryPointAddress, ignoreCase: true);
            Assert.False(provisioned.Stack.EntryPointWasFreshlyDeployed);
            Assert.Contains(log, line => line.Contains("already deployed on-chain"));

            Assert.False(string.IsNullOrEmpty(provisioned.Stack.Addresses.NethereumAccountFactoryAddress));
            Assert.False(string.IsNullOrEmpty(provisioned.Stack.Addresses.EcdsaValidatorAddress));
            Assert.False(string.IsNullOrEmpty(provisioned.Stack.WebAuthnConfig.ValidatorAddress));
            Assert.False(string.IsNullOrEmpty(provisioned.Stack.OwnableExecutor.ContractAddress));
            Assert.False(string.IsNullOrEmpty(provisioned.Stack.SocialRecoveryConfig.SocialRecoveryAddress));
            Assert.False(string.IsNullOrEmpty(provisioned.Stack.PoliciesConfig.SmartSessionAddress));
            Assert.False(string.IsNullOrEmpty(provisioned.Stack.Paymaster.Address));
            Assert.False(string.IsNullOrEmpty(provisioned.Stack.TestCounter.ContractAddress));
            Assert.False(string.IsNullOrEmpty(provisioned.Stack.BookingRegistry.ContractAddress));

            var recipient = EthECKey.GenerateKey().GetPublicAddress();
            var balanceBeforeFunding = await provisioned.Faucet.GetBalanceAsync(recipient);
            await provisioned.Faucet.FundAsync(recipient, 1m);
            var balanceAfterFunding = await provisioned.Faucet.GetBalanceAsync(recipient);

            Assert.Equal(BigInteger.Zero, balanceBeforeFunding);
            Assert.True(balanceAfterFunding > balanceBeforeFunding);
        }
    }
}
