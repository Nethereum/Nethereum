using Nethereum.X402.Client;
using Nethereum.X402.Models;
using System.Text.Json;

Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("x402 Payment Client - Manual vs Automatic Flow Demo");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine();

// IMPORTANT: Replace this with your own private key!
// This is a test key - DO NOT use in production or with real funds
const string PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
const string TOKEN_NAME = "USD Coin";
const string TOKEN_VERSION = "2";
const int CHAIN_ID = 31337; // Local Anvil
const string TOKEN_ADDRESS = "0x5FbDB2315678afecb367f032d93F642f64180aa3";
const string NETWORK = "localhost";

Console.WriteLine("Configuration:");
Console.WriteLine($"  Network: {NETWORK}");
Console.WriteLine($"  Chain ID: {CHAIN_ID}");
Console.WriteLine($"  Token: {TOKEN_NAME} v{TOKEN_VERSION}");
Console.WriteLine($"  Token Address: {TOKEN_ADDRESS}");
Console.WriteLine();

// ============================================================================
// DEMO 1: AUTOMATIC PAYMENT FLOW (Recommended - Like TypeScript Client)
// ============================================================================

Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("DEMO 1: AUTOMATIC PAYMENT FLOW");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine();
Console.WriteLine("The client automatically handles 402 responses:");
Console.WriteLine("  1. Makes initial request");
Console.WriteLine("  2. Receives 402 Payment Required");
Console.WriteLine("  3. Automatically creates and signs payment");
Console.WriteLine("  4. Retries request with X-PAYMENT header");
Console.WriteLine();

try
{
    var httpClient = new HttpClient();
    var options = new X402HttpClientOptions
    {
        PreferredNetwork = NETWORK,
        PreferredScheme = "exact",
        MaxPaymentAmount = 1.0m, // Max 1 USDC
        TokenName = TOKEN_NAME,
        TokenVersion = TOKEN_VERSION,
        ChainId = CHAIN_ID,
        TokenAddress = TOKEN_ADDRESS
    };

    var autoClient = new X402HttpClient(httpClient, PRIVATE_KEY, options);
    Console.WriteLine($"Client Address: {autoClient.Address}");
    Console.WriteLine();

    Console.WriteLine("[Automatic] Requesting premium content...");
    var response = await autoClient.GetAsync("http://localhost:5000/premium");

    Console.WriteLine($"Status: {response.StatusCode}");
    var content = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"Content: {content}");

    // Use extension method to get settlement details
    if (response.HasPaymentResponse())
    {
        var settlement = response.GetSettlementResponse();
        Console.WriteLine();
        Console.WriteLine("Payment Settlement (using extension methods):");
        Console.WriteLine($"  Success: {response.IsPaymentSuccessful()}");
        Console.WriteLine($"  Transaction: {response.GetTransactionHash()}");
        Console.WriteLine($"  Payer: {response.GetPayerAddress()}");
        Console.WriteLine($"  Network: {settlement?.Network}");
    }
}
catch (X402PaymentExceedsMaximumException ex)
{
    Console.WriteLine($"ERROR: Payment amount {ex.RequestedAmount} exceeds maximum {ex.MaximumAllowed}");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine();

// ============================================================================
// DEMO 2: MANUAL PAYMENT FLOW (Advanced - Full Control)
// ============================================================================

Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("DEMO 2: MANUAL PAYMENT FLOW");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine();
Console.WriteLine("Manually handle each step:");
Console.WriteLine("  1. Make request and receive 402");
Console.WriteLine("  2. Parse payment requirements");
Console.WriteLine("  3. Choose which payment option to use");
Console.WriteLine("  4. Call GetAsync with requirements");
Console.WriteLine();

try
{
    var httpClient = new HttpClient();
    var manualClient = new X402HttpClient(
        httpClient,
        PRIVATE_KEY,
        TOKEN_NAME,
        TOKEN_VERSION,
        CHAIN_ID,
        TOKEN_ADDRESS);

    Console.WriteLine($"Client Address: {manualClient.Address}");
    Console.WriteLine();

    // Step 1: Try without payment
    Console.WriteLine("[Manual Step 1] Requesting without payment...");
    var initialResponse = await httpClient.GetAsync("http://localhost:5000/premium");

    Console.WriteLine($"Status: {initialResponse.StatusCode}");

    // Step 2: Parse 402 response
    if (initialResponse.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
    {
        var paymentResponseJson = await initialResponse.Content.ReadAsStringAsync();
        var paymentResponse = JsonSerializer.Deserialize<PaymentRequirementsResponse>(
            paymentResponseJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Console.WriteLine();
        Console.WriteLine("[Manual Step 2] Payment required!");
        Console.WriteLine($"Available payment options: {paymentResponse?.Accepts?.Count}");

        if (paymentResponse?.Accepts != null && paymentResponse.Accepts.Count > 0)
        {
            // Step 3: Select payment requirements (use first option)
            var requirements = paymentResponse.Accepts[0];

            Console.WriteLine();
            Console.WriteLine("[Manual Step 3] Selected payment option:");
            Console.WriteLine($"  Network: {requirements.Network}");
            Console.WriteLine($"  Scheme: {requirements.Scheme}");
            Console.WriteLine($"  Amount: {requirements.MaxAmountRequired} atomic units");
            Console.WriteLine($"  Pay To: {requirements.PayTo}");
            Console.WriteLine($"  Asset: {requirements.Asset}");

            // Step 4: Make paid request
            Console.WriteLine();
            Console.WriteLine("[Manual Step 4] Sending payment...");
            var paidResponse = await manualClient.GetAsync(
                "http://localhost:5000/premium",
                requirements);

            Console.WriteLine($"Status: {paidResponse.StatusCode}");
            var paidContent = await paidResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"Content: {paidContent}");

            // Manual parsing of settlement (or use extension methods)
            if (paidResponse.Headers.Contains("X-PAYMENT-RESPONSE"))
            {
                var settlementHeader = paidResponse.Headers.GetValues("X-PAYMENT-RESPONSE").First();
                var settlementJson = System.Text.Encoding.UTF8.GetString(
                    Convert.FromBase64String(settlementHeader));
                var settlement = JsonSerializer.Deserialize<SettlementResponse>(
                    settlementJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                Console.WriteLine();
                Console.WriteLine("Payment Settlement (manual parsing):");
                Console.WriteLine($"  Success: {settlement?.Success}");
                Console.WriteLine($"  Transaction: {settlement?.Transaction}");
                Console.WriteLine($"  Payer: {settlement?.Payer}");
                Console.WriteLine($"  Network: {settlement?.Network}");
                if (!settlement!.Success)
                {
                    Console.WriteLine($"  Error: {settlement.ErrorReason}");
                }
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("Done!");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine();
Console.WriteLine("NOTE: Make sure the server is running:");
Console.WriteLine("  cd dotnet/examples/SimpleServer");
Console.WriteLine("  dotnet run");
Console.WriteLine();
