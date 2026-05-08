using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var chainId = int.TryParse(builder.Configuration["AppChain:ChainId"], out var cid) ? cid : 420420;

var config = new DevChainServerConfig
{
    Storage = "memory",
    ChainId = chainId
};

builder.Configuration.GetSection("AppChain").Bind(config);

builder.AddDevChainServer(config);

var app = builder.Build();

await app.MapDevChainEndpointsAsync();
app.MapDefaultEndpoints();

app.MapGet("/info", () => Results.Ok(new
{
    service = "Nethereum.AppChain.Node",
    chainId
}));

app.Run();
