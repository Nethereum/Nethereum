using Nethereum.RPC.Accounts;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Extensions;

var builder = WebApplication.CreateBuilder(args);

var facilitatorPrivateKey = builder.Configuration["X402:FacilitatorPrivateKey"]
    ?? throw new InvalidOperationException("FacilitatorPrivateKey not configured");

var rpcEndpointsByChainId = new Dictionary<int, string>
{
    { 11155111, builder.Configuration["X402:RpcEndpoints:Sepolia"] ?? "https://rpc.sepolia.org" },
    { 84532, builder.Configuration["X402:RpcEndpoints:BaseSepolia"] ?? "https://sepolia.base.org" }
};

builder.Services.AddControllers()
    .AddX402FacilitatorControllers();

var facilitatorAccount = new Account(facilitatorPrivateKey);
builder.Services.AddX402ExactProcessor(
    facilitatorAccount,
    rpcEndpointsByChainId);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
