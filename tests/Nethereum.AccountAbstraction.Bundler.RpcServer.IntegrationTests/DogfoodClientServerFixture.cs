using System.Numerics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.RpcServer.Configuration;
using Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.Signer;
using Nethereum.Web3;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    [CollectionDefinition(DogfoodClientServerFixture.COLLECTION_NAME)]
    public class DogfoodClientServerCollection : ICollectionFixture<DogfoodClientServerFixture> { }

    public class DogfoodClientServerFixture : IAsyncLifetime
    {
        public const string COLLECTION_NAME = "DogfoodClientServer";

        private readonly EthereumClientIntegrationFixture _ethereumFixture;
        private IHost? _host;

        public IWeb3 Web3 { get; private set; } = null!;
        public EntryPointService EntryPointService { get; private set; } = null!;
        public SimpleAccountFactoryService AccountFactoryService { get; private set; } = null!;
        public BundlerService BundlerService { get; private set; } = null!;

        public AccountAbstractionBundlerService BundlerClient { get; private set; } = null!;

        public string BeneficiaryAddress => EthereumClientIntegrationFixture.AccountAddress;
        public BigInteger ChainId => EthereumClientIntegrationFixture.ChainId;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public DogfoodClientServerFixture()
        {
            _ethereumFixture = new EthereumClientIntegrationFixture();
        }

        public async Task InitializeAsync()
        {
            Web3 = _ethereumFixture.GetWeb3();

            EntryPointService = await EntryPointService.DeployContractAndGetServiceAsync(
                Web3, new EntryPointDeployment());

            var factoryDeployment = new SimpleAccountFactoryDeployment
            {
                EntryPoint = EntryPointService.ContractAddress
            };

            AccountFactoryService = await SimpleAccountFactoryService.DeployContractAndGetServiceAsync(
                Web3, factoryDeployment);

            var bundlerConfig = new BundlerConfig
            {
                SupportedEntryPoints = new[] { EntryPointService.ContractAddress },
                BeneficiaryAddress = BeneficiaryAddress,
                MaxBundleSize = 10,
                MaxMempoolSize = 100,
                MinPriorityFeePerGas = 0,
                MaxBundleGas = 15_000_000,
                AutoBundleIntervalMs = 1_000,
                StrictValidation = false,
                SimulateValidation = true,
                UnsafeMode = true,
                ChainId = ChainId
            };

            BundlerService = new BundlerService(Web3, bundlerConfig);

            var serverConfig = new BundlerRpcServerConfig
            {
                ChainId = ChainId,
                SupportedEntryPoints = new[] { EntryPointService.ContractAddress },
                BeneficiaryAddress = BeneficiaryAddress,
                EnableDebugMethods = true,
                Verbose = false
            };

            _host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddRouting();
                            services.AddSingleton(serverConfig);
                            services.AddSingleton(BundlerService);
                            services.AddSingleton<IBundlerService>(BundlerService);
                            services.AddSingleton<IBundlerServiceExtended>(BundlerService);

                            services.AddSingleton<RpcHandlerRegistry>(provider =>
                            {
                                var bundler = provider.GetRequiredService<BundlerService>();
                                var registry = new RpcHandlerRegistry();
                                registry.AddBundlerHandlers(bundler);
                                registry.AddBundlerDebugHandlers(bundler);
                                return registry;
                            });

                            services.AddSingleton<RpcContext>(provider =>
                                new RpcContext(
                                    (Func<Nethereum.CoreChain.IChainNode>)(() => throw new InvalidOperationException(
                                        "The bundler RPC server has no in-process chain node")),
                                    serverConfig.ChainId,
                                    provider));

                            services.AddSingleton<RpcDispatcher>(provider =>
                            {
                                var registry = provider.GetRequiredService<RpcHandlerRegistry>();
                                var context = provider.GetRequiredService<RpcContext>();
                                return new RpcDispatcher(registry, context, null);
                            });
                        })
                        .Configure(app =>
                        {
                            var dispatcher = app.ApplicationServices.GetRequiredService<RpcDispatcher>();

                            app.UseRouting();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapPost("/", async context =>
                                {
                                    using var reader = new StreamReader(context.Request.Body);
                                    var json = await reader.ReadToEndAsync();

                                    var request = JsonSerializer.Deserialize<JsonRpcRequest>(json, _jsonOptions);
                                    if (request == null)
                                    {
                                        context.Response.StatusCode = 400;
                                        return;
                                    }

                                    var response = await dispatcher.DispatchAsync(request.ToRpcRequestMessage());
                                    context.Response.ContentType = "application/json";
                                    await context.Response.WriteAsync(
                                        JsonSerializer.Serialize(response.ToJsonRpcResponse(), _jsonOptions));
                                });
                            });
                        });
                })
                .StartAsync();

            var testHttpClient = _host.GetTestClient();
            var rpcClient = new RpcClient(testHttpClient.BaseAddress!, testHttpClient);
            BundlerClient = new AccountAbstractionBundlerService(rpcClient);
        }

        public async Task DisposeAsync()
        {
            if (_host != null)
                await _host.StopAsync();
            _host?.Dispose();
            BundlerService?.Dispose();
            _ethereumFixture?.Dispose();
        }

        public async Task<(string accountAddress, EthECKey accountKey)> CreateFundedAccountAsync(
            ulong salt,
            decimal ethAmount = 0.5m)
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();

            var result = await AccountFactoryService.CreateAndDeployAccountAsync(
                ownerAddress,
                ownerAddress,
                EntryPointService.ContractAddress,
                accountKey,
                ethAmount,
                salt);

            return (result.AccountAddress, accountKey);
        }
    }
}
