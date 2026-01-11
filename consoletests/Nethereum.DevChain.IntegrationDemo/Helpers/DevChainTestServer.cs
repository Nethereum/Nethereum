using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain;
using Nethereum.DevChain.Server.Accounts;
using Nethereum.DevChain.Server.Configuration;
using Nethereum.DevChain.Server.Server;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.DevChain.IntegrationDemo.Helpers;

public class DevChainTestServer : IAsyncDisposable
{
    private WebApplication? _app;
    private DevChainNode? _node;
    private DevAccountManager? _accountManager;
    private Task? _runTask;
    private CancellationTokenSource? _cts;

    public int Port { get; private set; }
    public string Url => $"http://127.0.0.1:{Port}";
    public DevChainNode? Node => _node;
    public DevAccountManager? AccountManager => _accountManager;

    public async Task StartAsync(DevChainServerConfig? config = null)
    {
        config ??= new DevChainServerConfig();
        Port = config.Port;

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDevChainServer(config);
        builder.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Warning);
        });

        _app = builder.Build();
        _node = _app.Services.GetRequiredService<DevChainNode>();
        _accountManager = _app.Services.GetRequiredService<DevAccountManager>();
        var dispatcher = _app.Services.GetRequiredService<RpcDispatcher>();

        await _node.StartAsync(_accountManager.Accounts.Select(a => a.Address));

        _app.MapPost("/", async (HttpContext httpContext) =>
        {
            try
            {
                using var reader = new StreamReader(httpContext.Request.Body);
                var json = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(json))
                {
                    httpContext.Response.StatusCode = 400;
                    await httpContext.Response.WriteAsJsonAsync(new { error = "Empty request body" });
                    return;
                }

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                if (json.TrimStart().StartsWith('['))
                {
                    var requests = JsonSerializer.Deserialize<RpcRequestMessage[]>(json, jsonOptions);
                    if (requests != null)
                    {
                        var responses = await dispatcher.DispatchBatchAsync(requests);
                        httpContext.Response.ContentType = "application/json";
                        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(responses));
                        return;
                    }
                }

                var request = JsonSerializer.Deserialize<RpcRequestMessage>(json, jsonOptions);
                if (request == null)
                {
                    httpContext.Response.StatusCode = 400;
                    await httpContext.Response.WriteAsJsonAsync(new { error = "Invalid JSON-RPC request" });
                    return;
                }

                var response = await dispatcher.DispatchAsync(request);
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
            catch (JsonException)
            {
                httpContext.Response.StatusCode = 400;
                httpContext.Response.ContentType = "application/json";
                var errorResponse = new { jsonrpc = "2.0", error = new { code = -32700, message = "Parse error" }, id = (object?)null };
                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
            }
            catch (Exception ex)
            {
                httpContext.Response.StatusCode = 500;
                httpContext.Response.ContentType = "application/json";
                var errorResponse = new { jsonrpc = "2.0", error = new { code = -32603, message = "Internal error: " + ex.Message }, id = (object?)null };
                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
            }
        });

        _app.MapGet("/", () => Results.Ok(new { status = "ok" }));

        _cts = new CancellationTokenSource();

        _runTask = Task.Run(async () =>
        {
            try
            {
                await _app.RunAsync($"http://127.0.0.1:{Port}");
            }
            catch (OperationCanceledException)
            {
            }
        });

        await WaitForServerReadyAsync();
    }

    private async Task WaitForServerReadyAsync(int maxRetries = 50)
    {
        using var client = new HttpClient();
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var response = await client.GetAsync(Url);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
            }
            await Task.Delay(100);
        }
        throw new Exception($"Server did not become ready at {Url}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }

        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        if (_runTask != null)
        {
            try
            {
                await _runTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
