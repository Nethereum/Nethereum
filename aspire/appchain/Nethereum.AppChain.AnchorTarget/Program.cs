using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var config = new DevChainServerConfig
{
    Storage = "memory",
    ChainId = int.TryParse(builder.Configuration["AnchorTarget:ChainId"], out var cid) ? cid : 1337
};

builder.AddDevChainServer(config);

var app = builder.Build();

await app.MapDevChainEndpointsAsync();
app.MapDefaultEndpoints();

app.MapGet("/info", () => Results.Ok(new
{
    service = "Nethereum.AppChain.AnchorTarget",
    chainId = config.ChainId
}));

app.Run();
