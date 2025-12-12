using Nethereum.X402.AspNetCore;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

var builder = WebApplication.CreateBuilder(args);

// Register facilitator client
builder.Services.AddHttpClient<IFacilitatorClient, HttpFacilitatorClient>(client =>
{
    // You can configure HttpClient settings here if needed
});

builder.Services.AddSingleton<IFacilitatorClient>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();
    return new HttpFacilitatorClient(httpClient, "https://x402.org/facilitator");
});

var app = builder.Build();

// Configure x402 payment middleware
app.UseX402(options =>
{
    options.FacilitatorUrl = "https://x402.org/facilitator";

    // Protect the /premium endpoint with payment
    options.Routes.Add(new RoutePaymentConfig(
        pathPattern: "/premium",
        requirements: new PaymentRequirements
        {
            Scheme = "exact",
            Network = "base-sepolia",
            MaxAmountRequired = "10000", // 0.01 USDC (6 decimals)
            Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e", // USDC on Base Sepolia
            PayTo = "0x857b06519E91e3A54538791bDbb0E22373e36b66", // YOUR wallet address
            Resource = "/premium",
            Description = "Premium API access",
            MimeType = "application/json",
            MaxTimeoutSeconds = 60
        }
    ));
});

// Free endpoint - no payment required
app.MapGet("/free", () =>
{
    return Results.Json(new
    {
        message = "This is a free endpoint!",
        timestamp = DateTime.UtcNow
    });
});

// Premium endpoint - requires payment (configured above)
app.MapGet("/premium", () =>
{
    return Results.Json(new
    {
        message = "Welcome to premium content!",
        secretData = "This data costs $0.01 in USDC",
        value = 42,
        timestamp = DateTime.UtcNow
    });
});

Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine("x402 Payment Server Running");
Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine();
Console.WriteLine("Endpoints:");
Console.WriteLine("  - GET http://localhost:5000/free     (Free, no payment)");
Console.WriteLine("  - GET http://localhost:5000/premium  (Requires 0.01 USDC)");
Console.WriteLine();
Console.WriteLine("Facilitator: https://x402.org/facilitator");
Console.WriteLine("Network: base-sepolia");
Console.WriteLine();
Console.WriteLine("Try it:");
Console.WriteLine("  curl http://localhost:5000/free");
Console.WriteLine("  curl http://localhost:5000/premium");
Console.WriteLine();
Console.WriteLine("=".PadRight(60, '='));

app.Run("http://localhost:5000");
