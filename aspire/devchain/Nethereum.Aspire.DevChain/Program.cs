using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var config = new DevChainServerConfig();
builder.Configuration.GetSection("DevChain").Bind(config);

if (config.Storage == null)
    config.Storage = "memory";

if (config.ChainId == 0)
    config.ChainId = 31337;

builder.AddDevChainServer(config);

var app = builder.Build();

app.MapDevChainEndpoints();
app.MapDefaultEndpoints();

app.Run();
