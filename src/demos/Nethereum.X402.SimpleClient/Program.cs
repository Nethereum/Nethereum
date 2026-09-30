using Nethereum.X402.Client;
using Nethereum.X402.Models;
using System.Text.Json;

Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("x402 Payment Client - Manual vs Automatic Flow Demo");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine();

const string PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
const string NETWORK = "eip155:84532";

Console.WriteLine("Configuration:");
Console.WriteLine($"  Preferred Network: {NETWORK}");
Console.WriteLine();


Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("DEMO 1: AUTOMATIC PAYMENT FLOW");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine();
Console.WriteLine("The client automatically handles 402 responses:");
Console.WriteLine("  1. Makes initial request");
Console.WriteLine("  2. Receives 402 Payment Required");
Console.WriteLine("  3. Automatically creates and signs payment");
Console.WriteLine("  4. Retries request with PAYMENT-SIGNATURE header");
Console.WriteLine();

try
{
    var httpClient = new HttpClient();
    var options = new X402HttpClientOptions
    {
        PreferredNetwork = NETWORK,
        PreferredScheme = "exact",
        MaxAmount = "1000000"
    };

    var autoClient = new X402HttpClient(httpClient, PRIVATE_KEY, options);
    Console.WriteLine($"Client Address: {autoClient.Address}");
    Console.WriteLine();

    Console.WriteLine("[Automatic] Requesting premium content...");
    var response = await autoClient.GetAsync("http://localhost:5000/premium");

    Console.WriteLine($"Status: {response.StatusCode}");
    var content = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"Content: {content}");

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
    var manualClient = new X402HttpClient(httpClient, PRIVATE_KEY);

    Console.WriteLine($"Client Address: {manualClient.Address}");
    Console.WriteLine();

    Console.WriteLine("[Manual Step 1] Requesting without payment...");
    var initialResponse = await httpClient.GetAsync("http://localhost:5000/premium");

    Console.WriteLine($"Status: {initialResponse.StatusCode}");

    if (initialResponse.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
    {
        var paymentResponseJson = await initialResponse.Content.ReadAsStringAsync();
        var paymentResponse = JsonSerializer.Deserialize<PaymentRequired>(
            paymentResponseJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Console.WriteLine();
        Console.WriteLine("[Manual Step 2] Payment required!");
        Console.WriteLine($"Available payment options: {paymentResponse?.Accepts?.Count}");

        if (paymentResponse?.Accepts != null && paymentResponse.Accepts.Count > 0)
        {
            var requirements = paymentResponse.Accepts[0];

            Console.WriteLine();
            Console.WriteLine("[Manual Step 3] Selected payment option:");
            Console.WriteLine($"  Network: {requirements.Network}");
            Console.WriteLine($"  Scheme: {requirements.Scheme}");
            Console.WriteLine($"  Amount: {requirements.Amount} atomic units");
            Console.WriteLine($"  Pay To: {requirements.PayTo}");
            Console.WriteLine($"  Asset: {requirements.Asset}");

            Console.WriteLine();
            Console.WriteLine("[Manual Step 4] Sending payment...");
            var paidResponse = await manualClient.GetAsync(
                "http://localhost:5000/premium",
                requirements);

            Console.WriteLine($"Status: {paidResponse.StatusCode}");
            var paidContent = await paidResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"Content: {paidContent}");

            if (paidResponse.Headers.Contains("PAYMENT-RESPONSE"))
            {
                var settlementHeader = paidResponse.Headers.GetValues("PAYMENT-RESPONSE").First();
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
Console.WriteLine("  cd src/demos/Nethereum.X402.SimpleServer");
Console.WriteLine("  dotnet run");
Console.WriteLine();
