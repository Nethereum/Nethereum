using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain;
using Nethereum.DevChain.Accounts;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Fixtures
{
    public class DevChainHttpFixture : IAsyncLifetime
    {
        private WebApplication? _app;
        private Task? _runTask;

        public const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        public const string Address = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        public const int ChainId = 31337;
        public const int Port = 18545;
        public string Url => $"http://127.0.0.1:{Port}";

        public const string RecipientAddress = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";

        public Nethereum.Web3.Web3 Web3 { get; private set; } = null!;
        public Nethereum.Web3.Accounts.Account Account { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            var config = new DevChainServerConfig
            {
                Port = Port,
                ChainId = ChainId
            };

            var builder = WebApplication.CreateBuilder();
            builder.Services.AddDevChainServer(config);
            builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

            _app = builder.Build();

            var node = _app.Services.GetRequiredService<DevChainNode>();
            var accountManager = _app.Services.GetRequiredService<DevAccountManager>();
            var dispatcher = _app.Services.GetRequiredService<RpcDispatcher>();

            await node.StartAsync(accountManager.Accounts.Select(a => a.Address));

            _app.MapPost("/", async (HttpContext httpContext) =>
            {
                try
                {
                    using var reader = new StreamReader(httpContext.Request.Body);
                    var json = await reader.ReadToEndAsync();

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        httpContext.Response.StatusCode = 400;
                        await httpContext.Response.WriteAsync("{\"error\":\"Empty request body\"}");
                        return;
                    }

                    httpContext.Response.ContentType = "application/json";

                    if (json.TrimStart().StartsWith('['))
                    {
                        var requests = JsonSerializer.Deserialize(json, CoreChainJsonContext.Default.JsonRpcRequestArray);
                        if (requests != null)
                        {
                            var rpcRequests = requests.Select(ToRpcRequestMessage).ToArray();
                            var responses = await dispatcher.DispatchBatchAsync(rpcRequests);
                            var jsonResponses = responses.Select(ToJsonRpcResponse).ToArray();
                            await httpContext.Response.WriteAsync(
                                JsonSerializer.Serialize(jsonResponses, CoreChainJsonContext.Default.JsonRpcResponseArray));
                            return;
                        }
                    }

                    var request = JsonSerializer.Deserialize(json, CoreChainJsonContext.Default.JsonRpcRequest);
                    if (request == null)
                    {
                        httpContext.Response.StatusCode = 400;
                        await httpContext.Response.WriteAsync("{\"error\":\"Invalid JSON-RPC request\"}");
                        return;
                    }

                    var rpcRequest = ToRpcRequestMessage(request);
                    var response = await dispatcher.DispatchAsync(rpcRequest);
                    var jsonResponse = ToJsonRpcResponse(response);
                    await httpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(jsonResponse, CoreChainJsonContext.Default.JsonRpcResponse));
                }
                catch (JsonException)
                {
                    httpContext.Response.StatusCode = 400;
                    httpContext.Response.ContentType = "application/json";
                    var errorResponse = new JsonRpcResponse
                    {
                        Id = null,
                        Error = new JsonRpcError { Code = -32700, Message = "Parse error" }
                    };
                    await httpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(errorResponse, CoreChainJsonContext.Default.JsonRpcResponse));
                }
                catch (Exception ex)
                {
                    httpContext.Response.StatusCode = 500;
                    httpContext.Response.ContentType = "application/json";
                    var errorResponse = new JsonRpcResponse
                    {
                        Id = null,
                        Error = new JsonRpcError { Code = -32603, Message = "Internal error: " + ex.Message }
                    };
                    await httpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(errorResponse, CoreChainJsonContext.Default.JsonRpcResponse));
                }
            });

            _runTask = Task.Run(async () =>
            {
                try { await _app.RunAsync($"http://127.0.0.1:{Port}"); }
                catch (OperationCanceledException) { }
            });

            await WaitForServerReadyAsync();

            Account = new Nethereum.Web3.Accounts.Account(PrivateKey, ChainId);
            Account.TransactionManager.UseLegacyAsDefault = true;
            Web3 = new Nethereum.Web3.Web3(Account, Url);
        }

        public async Task DisposeAsync()
        {
            if (_app != null)
            {
                await _app.StopAsync();
                await _app.DisposeAsync();
            }

            if (_runTask != null)
            {
                try { await _runTask; }
                catch (OperationCanceledException) { }
            }
        }

        private async Task WaitForServerReadyAsync(int maxRetries = 50)
        {
            using var client = new HttpClient();
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var content = new StringContent(
                        "{\"jsonrpc\":\"2.0\",\"method\":\"eth_chainId\",\"params\":[],\"id\":1}",
                        System.Text.Encoding.UTF8,
                        "application/json");
                    var response = await client.PostAsync(Url, content);
                    if (response.IsSuccessStatusCode) return;
                }
                catch { }
                await Task.Delay(100);
            }
            throw new Exception($"Server did not become ready at {Url}");
        }

        private static RpcRequestMessage ToRpcRequestMessage(JsonRpcRequest request)
        {
            return new RpcRequestMessage
            {
                Id = request.Id,
                Method = request.Method,
                JsonRpcVersion = request.Jsonrpc,
                RawParameters = request.Params.HasValue ? request.Params.Value : null
            };
        }

        private static JsonRpcResponse ToJsonRpcResponse(RpcResponseMessage response)
        {
            if (response.HasError)
            {
                return new JsonRpcResponse
                {
                    Id = response.Id,
                    Error = new JsonRpcError
                    {
                        Code = response.Error.Code,
                        Message = response.Error.Message,
                        Data = response.Error.Data
                    }
                };
            }

            return new JsonRpcResponse
            {
                Id = response.Id,
                Result = response.Result
            };
        }
    }
}
