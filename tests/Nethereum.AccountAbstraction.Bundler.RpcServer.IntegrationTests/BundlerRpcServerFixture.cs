using System.Net.Http.Json;
using System.Numerics;
using System.Text;
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
using Nethereum.AccountAbstraction.Structs;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Signer;
using Nethereum.Web3;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    [CollectionDefinition(BundlerRpcServerFixture.COLLECTION_NAME)]
    public class BundlerRpcServerCollection : ICollectionFixture<BundlerRpcServerFixture> { }

    public class BundlerRpcServerFixture : IAsyncLifetime
    {
        public const string COLLECTION_NAME = "BundlerRpcServer";

        private readonly EthereumClientIntegrationFixture _ethereumFixture;
        private IHost? _host;

        public IWeb3 Web3 { get; private set; } = null!;
        public EntryPointService EntryPointService { get; private set; } = null!;
        public SimpleAccountFactoryService AccountFactoryService { get; private set; } = null!;
        public BundlerService BundlerService { get; private set; } = null!;
        public HttpClient RpcClient { get; private set; } = null!;

        public string BeneficiaryAddress => EthereumClientIntegrationFixture.AccountAddress;
        public string OperatorPrivateKey => EthereumClientIntegrationFixture.AccountPrivateKey;
        public EthECKey OperatorKey { get; private set; } = null!;
        public BigInteger ChainId => EthereumClientIntegrationFixture.ChainId;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public BundlerRpcServerFixture()
        {
            _ethereumFixture = new EthereumClientIntegrationFixture();
        }

        public async Task InitializeAsync()
        {
            Web3 = _ethereumFixture.GetWeb3();
            OperatorKey = new EthECKey(OperatorPrivateKey);

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
                AutoBundleIntervalMs = 0,
                StrictValidation = false,
                SimulateValidation = false,
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
                                new RpcContext(null!, serverConfig.ChainId, provider));

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

                                    var request = JsonSerializer.Deserialize<RpcRequestMessage>(json, _jsonOptions);
                                    if (request == null)
                                    {
                                        context.Response.StatusCode = 400;
                                        return;
                                    }

                                    var response = await dispatcher.DispatchAsync(request);
                                    context.Response.ContentType = "application/json";
                                    await context.Response.WriteAsync(JsonSerializer.Serialize(response, _jsonOptions));
                                });

                                endpoints.MapGet("/", async context =>
                                {
                                    await context.Response.WriteAsJsonAsync(new { status = "ok" });
                                });
                            });
                        });
                })
                .StartAsync();

            RpcClient = _host.GetTestClient();
        }

        public async Task DisposeAsync()
        {
            RpcClient?.Dispose();
            if (_host != null)
                await _host.StopAsync();
            _host?.Dispose();
            BundlerService?.Dispose();
            _ethereumFixture?.Dispose();
        }

        public async Task<JsonRpcResponse> SendRpcRequestAsync(string method, params object[] parameters)
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = method,
                @params = parameters
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await RpcClient.PostAsync("/", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonRpcResponse>(responseJson, _jsonOptions)!;
        }

        public async Task<(string accountAddress, EthECKey accountKey)> CreateFundedAccountAsync(
            ulong salt,
            decimal ethAmount = 0.5m)
        {
            var accountKey = new EthECKey(TestAccounts.Account2PrivateKey);
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

        public async Task<string> GetAccountAddressAsync(string owner, ulong salt)
        {
            return await AccountFactoryService.GetAddressQueryAsync(owner, salt);
        }

        public async Task FundAccountAsync(string address, decimal ethAmount)
        {
            await Web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(address, ethAmount);
        }

        public async Task<PackedUserOperation> CreateSignedUserOperationAsync(
            string sender,
            EthECKey signerKey,
            byte[]? callData = null)
        {
            var userOp = new UserOperation
            {
                Sender = sender,
                CallData = callData ?? Array.Empty<byte>(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            return await EntryPointService.SignAndInitialiseUserOperationAsync(userOp, signerKey);
        }

        public class JsonRpcResponse
        {
            public string Jsonrpc { get; set; } = "2.0";
            public object? Id { get; set; }
            public JsonElement? Result { get; set; }
            public JsonRpcError? Error { get; set; }
        }

        public class JsonRpcError
        {
            public int Code { get; set; }
            public string Message { get; set; } = "";
            public object? Data { get; set; }
        }
    }
}
