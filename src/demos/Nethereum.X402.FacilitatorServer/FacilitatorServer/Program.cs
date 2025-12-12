using Nethereum.RPC.Accounts;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Read configuration
var facilitatorPrivateKey = builder.Configuration["X402:FacilitatorPrivateKey"]
    ?? throw new InvalidOperationException("FacilitatorPrivateKey not configured");

// Configure RPC endpoints for different networks
var rpcEndpoints = new Dictionary<string, string>
{
    { "sepolia", builder.Configuration["X402:RpcEndpoints:Sepolia"] ?? "https://rpc.sepolia.org" },
    { "base-sepolia", builder.Configuration["X402:RpcEndpoints:BaseSepolia"] ?? "https://sepolia.base.org" }
};

// Configure token addresses for each network
var tokenAddresses = new Dictionary<string, string>
{
    { "sepolia", builder.Configuration["X402:TokenAddresses:Sepolia"] ?? "" },
    { "base-sepolia", builder.Configuration["X402:TokenAddresses:BaseSepolia"] ?? "" }
};

// Configure chain IDs
var chainIds = new Dictionary<string, int>
{
    { "sepolia", 11155111 },
    { "base-sepolia", 84532 }
};

// Configure token names (for EIP-712 domain)
var tokenNames = new Dictionary<string, string>
{
    { "sepolia", builder.Configuration["X402:TokenNames:Sepolia"] ?? "USD Coin" },
    { "base-sepolia", builder.Configuration["X402:TokenNames:BaseSepolia"] ?? "USD Coin" }
};

// Configure token versions (for EIP-712 domain)
var tokenVersions = new Dictionary<string, string>
{
    { "sepolia", builder.Configuration["X402:TokenVersions:Sepolia"] ?? "2" },
    { "base-sepolia", builder.Configuration["X402:TokenVersions:BaseSepolia"] ?? "2" }
};

// Add services to the container
builder.Services.AddControllers()
    .AddX402FacilitatorControllers(); // Register facilitator controllers

// Option 1: Register with private key directly (converts to IAccount internally)
// builder.Services.AddX402TransferProcessor(
//     facilitatorPrivateKey,
//     rpcEndpoints,
//     tokenAddresses,
//     chainIds,
//     tokenNames,
//     tokenVersions);

// Option 2: Register with IAccount instance (recommended for production)
var facilitatorAccount = new Account(facilitatorPrivateKey);
builder.Services.AddX402TransferProcessor(
    facilitatorAccount,
    rpcEndpoints,
    tokenAddresses,
    chainIds,
    tokenNames,
    tokenVersions);

// Option 3: Register with factory function (for advanced scenarios)
// This allows resolving the account from other services at runtime
// builder.Services.AddX402TransferProcessor(
//     sp => {
//         // Could retrieve from secure key management service, hardware wallet, etc.
//         var config = sp.GetRequiredService<IConfiguration>();
//         var pk = config["X402:FacilitatorPrivateKey"];
//         return new Account(pk);
//     },
//     rpcEndpoints,
//     tokenAddresses,
//     chainIds,
//     tokenNames,
//     tokenVersions);

// Add Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
