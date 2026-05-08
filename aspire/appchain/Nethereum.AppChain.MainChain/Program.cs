using Nethereum.AppChain.MainChain;
using Nethereum.DevChain.Configuration;
using Nethereum.DevChain.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var config = new DevChainServerConfig
{
    Storage = "memory",
    ChainId = int.TryParse(builder.Configuration["MainChain:ChainId"], out var cid) ? cid : 1337
};

builder.AddDevChainServer(config);

var contractAddresses = new ContractAddresses();
builder.Services.AddSingleton(contractAddresses);
builder.Services.AddHostedService<MainChainBootstrapService>();

var app = builder.Build();

await app.MapDevChainEndpointsAsync();
app.MapDefaultEndpoints();

app.MapGet("/info", () => Results.Ok(new
{
    service = "Nethereum.AppChain.MainChain",
    chainId = config.ChainId
}));

app.MapGet("/contracts", () =>
{
    if (!contractAddresses.IsReady)
        return Results.StatusCode(503);
    return Results.Ok(new
    {
        anchor = contractAddresses.Anchor,
        authority = contractAddresses.Authority,
        appChainId = contractAddresses.AppChainId
    });
});

app.Run();
