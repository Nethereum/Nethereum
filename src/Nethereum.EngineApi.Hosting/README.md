# Nethereum.EngineApi.Hosting

Serve the Ethereum Engine API (newPayload, forkchoiceUpdated, getPayload) over the JWT-authenticated authrpc port, the interface a consensus client such as Lighthouse uses to drive an execution client.

It hosts an `IEngineApiService` that you supply, so the same endpoint fronts the DevChain block producer and the mainnet follower.

```csharp
services.AddEngineApiServer(
    new EngineApiServerConfig { Port = 8551, JwtSecretPath = "./jwt.hex" },
    provider => engineApiService);

app.MapEngineApiEndpoints();
```

`AddEngineApiServer` reads or creates the shared secret at `JwtSecretPath` (hex, 32 bytes). Requests on the Engine port must carry an HS256 `Authorization: Bearer` token whose `iat` is within 60 seconds. The public RPC port is never served Engine methods.

The host resolves the `RpcContext` from the service provider, so register the chain's `RpcContext` first.

## Registration

```csharp
public static IServiceCollection AddEngineApiServer(
    this IServiceCollection services,
    EngineApiServerConfig config,
    Func<IServiceProvider, IEngineApiService> engineFactory,
    bool verbose = false)

public static WebApplication MapEngineApiEndpoints(this WebApplication app)
```

`AddEngineApiServer` registers the `EngineApiServerConfig`, the `IEngineApiService` your factory returns, an `EngineJwtValidator` built from the secret file, and an `EngineApiHost` (the Engine handler registry, its `RpcDispatcher` and the validator). The `WebApplicationBuilder` overload does the same on `builder.Services`. `verbose` gives the Engine dispatcher a logger.

`MapEngineApiEndpoints` adds `http://{BindAddress}:{Port}` to the application's URLs and answers on that port only: a request without a valid token gets `401 {"error":"unauthorized"}`, a non-POST request gets `404`, and a POST body is dispatched to the Engine handlers. Requests on any other port pass through to the rest of the application.

`EngineApiServerConfig`:

| Property | Default |
|---|---|
| `Port` | `8551` (`EngineApiServerConfig.DefaultPort`) |
| `BindAddress` | `"127.0.0.1"` |
| `JwtSecretPath` | `"./jwt.hex"` (`EngineApiServerConfig.DefaultJwtSecretFileName`) |

## The shared secret and the token

`EngineJwtSecret.LoadOrCreate(path)` loads the secret file consensus clients share. The file holds exactly 32 bytes as hex, optionally prefixed with `0x`; an empty, short, non-hex or oversized file throws `InvalidDataException`, so an empty key can never validate a token. A missing file is created: the new secret is written to a temporary file and moved into place, and on Unix the file is readable and writable by its owner only.

`EngineJwtValidator.TryValidate(authorizationHeaderValue, out error)` accepts a `Bearer` token only when the header names the `HS256` algorithm, the signature matches (compared in constant time), and the `iat` claim is present and within 60 seconds of the clock. A token whose header or claims are valid JSON of the wrong shape is refused with `false`, not an exception, so the caller answers `401`.

## Used by

- `Nethereum.DevChain`: `AddDevChainServer` registers the Engine API when `DevChainServerConfig.EngineApiEnabled` is set, with the secret at `<DataDir>/jwt.hex` unless `EngineJwtSecretPath` names another.
- `Nethereum.MainnetChain`: `--engine` (`MainnetChain:Engine:Enabled`) registers the follower's Engine service through `MainnetEngineComposition.AddMainnetEngine`, with `--engine-port` and `--engine-jwt-secret`.
