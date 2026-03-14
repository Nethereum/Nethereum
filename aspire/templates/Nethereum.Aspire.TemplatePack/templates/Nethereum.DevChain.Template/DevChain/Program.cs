using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain;
using Nethereum.DevChain.Server.Accounts;
using Nethereum.DevChain.Server.Configuration;
using Nethereum.DevChain.Server.Hosting;
using Nethereum.DevChain.Server.Server;
using Nethereum.JsonRpc.Client.RpcMessages;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var config = new DevChainServerConfig();
builder.Configuration.GetSection("DevChain").Bind(config);

if (config.Storage == null)
    config.Storage = "sqlite";

if (config.ChainId == 0)
    config.ChainId = CHAIN_ID_VALUE;

builder.Services.AddDevChainServer(config);
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddHostedService(sp =>
    new DevChainHostedService(
        sp.GetRequiredService<DevChainNode>(),
        sp.GetRequiredService<DevAccountManager>(),
        sp,
        sp.GetService<ILoggerFactory>()?.CreateLogger<DevChainHostedService>()));

var app = builder.Build();

var dispatcher = app.Services.GetRequiredService<RpcDispatcher>();

app.UseCors();

app.MapPost("/", async (HttpContext httpContext) =>
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

app.MapGet("/", () => Results.Ok(new { status = "ok", service = "Nethereum.DevChain" }));

app.MapDefaultEndpoints();

app.Run();

static RpcRequestMessage ToRpcRequestMessage(JsonRpcRequest request)
{
    return new RpcRequestMessage
    {
        Id = request.Id,
        Method = request.Method,
        JsonRpcVersion = request.Jsonrpc,
        RawParameters = request.Params.HasValue ? request.Params.Value : null
    };
}

static JsonRpcResponse ToJsonRpcResponse(RpcResponseMessage response)
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
