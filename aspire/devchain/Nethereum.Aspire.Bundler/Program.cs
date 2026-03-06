using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Bundler.RpcServer.Configuration;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var devchainUrl = builder.Configuration["services:devchain:http:0"]
    ?? builder.Configuration["services:devchain:https:0"]
    ?? builder.Configuration["Bundler:RpcUrl"]
    ?? "http://localhost:8545";

var chainId = int.TryParse(builder.Configuration["Bundler:ChainId"], out var cid) ? cid : 31337;

var config = new BundlerRpcServerConfig
{
    RpcUrl = devchainUrl,
    ChainId = chainId,
    EnableDebugMethods = true
};

builder.Services.AddBundlerRpcServer(config);
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

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
        await httpContext.Response.WriteAsync("{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32700,\"message\":\"Parse error\"},\"id\":null}");
    }
    catch (Exception ex)
    {
        httpContext.Response.StatusCode = 500;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsync($"{{\"jsonrpc\":\"2.0\",\"error\":{{\"code\":-32603,\"message\":\"Internal error: {ex.Message}\"}},\"id\":null}}");
    }
});

app.MapGet("/", () => Results.Ok(new { status = "ok", service = "Nethereum.Bundler" }));

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
