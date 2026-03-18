using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var config = new DevChainServerConfig();
builder.Configuration.GetSection("DevChain").Bind(config);

if (config.Storage == null)
    config.Storage = "sqlite";

if (config.ChainId == 0)
    config.ChainId = CHAIN_ID_VALUE;

builder.AddDevChainServer(config);

var app = builder.Build();

await app.MapDevChainEndpointsAsync();
app.MapDefaultEndpoints();

app.Run();
