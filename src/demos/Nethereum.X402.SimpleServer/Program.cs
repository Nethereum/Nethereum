using Nethereum.X402.AspNetCore;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<IFacilitatorClient, HttpFacilitatorClient>(client =>
{
});

builder.Services.AddSingleton<IFacilitatorClient>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();
    return new HttpFacilitatorClient(httpClient, "https://x402.org/facilitator");
});

var app = builder.Build();

app.UseX402(options =>
{
    options.FacilitatorUrl = "https://x402.org/facilitator";

    options.Routes.Add(new RoutePaymentConfig(
        pathPattern: "/premium",
        requirements: new PaymentRequirements
        {
            Scheme = "exact",
            Network = "eip155:84532",
            Amount = "10000",
            Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
            PayTo = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
            MaxTimeoutSeconds = 60,
            Extra = new ExactSchemeExtra
            {
                AssetTransferMethod = "eip3009",
                Name = "USDC",
                Version = "2"
            }
        }
    ));

    options.Routes.Add(new RoutePaymentConfig(
        pathPattern: "/premium-permit2",
        requirements: new PaymentRequirements
        {
            Scheme = "exact",
            Network = "eip155:84532",
            Amount = "10000",
            Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
            PayTo = "0x857b06519E91e3A54538791bDbb0E22373e36b66",
            MaxTimeoutSeconds = 60,
            Extra = new ExactSchemeExtra { AssetTransferMethod = "permit2" }
        }
    ));
});

app.MapGet("/free", () =>
{
    return Results.Json(new
    {
        message = "This is a free endpoint!",
        timestamp = DateTime.UtcNow
    });
});

app.MapGet("/premium", () =>
{
    return Results.Json(new
    {
        message = "Welcome to premium content!",
        secretData = "This data costs $0.01 in USDC (EIP-3009)",
        value = 42,
        timestamp = DateTime.UtcNow
    });
});

app.MapGet("/premium-permit2", () =>
{
    return Results.Json(new
    {
        message = "Welcome to premium content!",
        secretData = "This data costs $0.01 in USDC (Permit2)",
        value = 42,
        timestamp = DateTime.UtcNow
    });
});

Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine("x402 Payment Server Running");
Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine();
Console.WriteLine("Endpoints:");
Console.WriteLine("  - GET http://localhost:5000/free            (Free, no payment)");
Console.WriteLine("  - GET http://localhost:5000/premium         (0.01 USDC via EIP-3009)");
Console.WriteLine("  - GET http://localhost:5000/premium-permit2 (0.01 USDC via Permit2)");
Console.WriteLine();
Console.WriteLine("Facilitator: https://x402.org/facilitator");
Console.WriteLine("Network: eip155:84532 (Base Sepolia)");
Console.WriteLine();
Console.WriteLine("Try it:");
Console.WriteLine("  curl http://localhost:5000/free");
Console.WriteLine("  curl http://localhost:5000/premium");
Console.WriteLine("  curl http://localhost:5000/premium-permit2");
Console.WriteLine();
Console.WriteLine("=".PadRight(60, '='));

app.Run("http://localhost:5000");
