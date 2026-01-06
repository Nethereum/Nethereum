using System.Text.Json;
using System.Text.Json.Serialization;
using Nethereum.DataServices.CoinGecko;

// Path from bin/Debug/net8.0 (5 levels up to Nethereum root) -> src/Nethereum.TokenServices/Resources
var resourcesPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Nethereum.TokenServices", "Resources"));

Console.WriteLine($"Resources path: {resourcesPath}");

if (args.Length == 0 || args[0] == "help")
{
    Console.WriteLine("Nethereum TokenServices Refresh Tool");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  convert         - Convert existing JSON files to minimal tokenlists.org format");
    Console.WriteLine("  refresh-tokens  - Download fresh token lists from CoinGecko");
    Console.WriteLine("  refresh-coins   - Download CoinGecko coin ID mappings");
    Console.WriteLine("  refresh-platforms - Download CoinGecko asset platforms");
    Console.WriteLine("  refresh-all     - Refresh all data");
    return;
}

var command = args[0].ToLower();

switch (command)
{
    case "convert":
        await ConvertToMinimalFormat(resourcesPath);
        break;
    case "refresh-tokens":
        await RefreshTokenLists(resourcesPath);
        break;
    case "refresh-coins":
        await RefreshCoinMappings(resourcesPath);
        break;
    case "refresh-platforms":
        await RefreshPlatforms(resourcesPath);
        break;
    case "refresh-all":
        await RefreshPlatforms(resourcesPath);
        await RefreshTokenLists(resourcesPath);
        await RefreshCoinMappings(resourcesPath);
        break;
    default:
        Console.WriteLine($"Unknown command: {command}");
        break;
}

async Task ConvertToMinimalFormat(string path)
{
    Console.WriteLine("Converting existing JSON files to minimal tokenlists.org format...");

    var files = Directory.GetFiles(path, "tokenlist_*.json");
    var options = new JsonSerializerOptions
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    foreach (var file in files)
    {
        Console.WriteLine($"Processing: {Path.GetFileName(file)}");

        var json = await File.ReadAllTextAsync(file);
        var tokens = JsonSerializer.Deserialize<List<TokenFull>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (tokens == null) continue;

        var minimal = tokens.Select(t => new TokenMinimal
        {
            ChainId = t.ChainId,
            Address = t.Address,
            Name = t.Name,
            Symbol = t.Symbol,
            Decimals = t.Decimals
        }).ToList();

        var minimalJson = JsonSerializer.Serialize(minimal, options);
        await File.WriteAllTextAsync(file, minimalJson);

        var originalSize = json.Length;
        var newSize = minimalJson.Length;
        var savings = (1 - (double)newSize / originalSize) * 100;

        Console.WriteLine($"  {tokens.Count} tokens, {originalSize:N0} -> {newSize:N0} bytes ({savings:F1}% smaller)");
    }

    Console.WriteLine("Done!");
}

async Task RefreshTokenLists(string path)
{
    var chainIds = new long[] { 1, 10, 56, 100, 137, 324, 8453, 42161, 42220, 43114, 59144 };
    var coinGecko = new CoinGeckoApiService();
    var options = new JsonSerializerOptions
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    Console.WriteLine("Refreshing token lists from CoinGecko...");

    foreach (var chainId in chainIds)
    {
        Console.WriteLine($"Fetching chain {chainId}...");

        try
        {
            var tokens = await coinGecko.GetTokensForChainAsync(chainId);
            if (tokens == null || tokens.Count == 0)
            {
                Console.WriteLine($"  No tokens found for chain {chainId}");
                continue;
            }

            var minimal = tokens.Select(t => new TokenMinimal
            {
                ChainId = chainId,
                Address = t.Address,
                Name = t.Name,
                Symbol = t.Symbol,
                Decimals = t.Decimals
            }).ToList();

            var filePath = Path.Combine(path, $"tokenlist_{chainId}.json");
            var json = JsonSerializer.Serialize(minimal, options);
            await File.WriteAllTextAsync(filePath, json);

            Console.WriteLine($"  Saved {tokens.Count} tokens to {Path.GetFileName(filePath)}");

            // Rate limit: wait between requests
            await Task.Delay(2000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error: {ex.Message}");
        }
    }

    Console.WriteLine("Done!");
}

async Task RefreshCoinMappings(string path)
{
    var coingeckoPath = Path.Combine(path, "coingecko");
    Directory.CreateDirectory(coingeckoPath);

    var chainIds = new long[] { 1, 10, 56, 100, 137, 324, 8453, 42161, 42220, 43114, 59144 };
    var coinGecko = new CoinGeckoApiService();
    var options = new JsonSerializerOptions { WriteIndented = false };

    Console.WriteLine("Refreshing CoinGecko coin ID mappings...");

    foreach (var chainId in chainIds)
    {
        Console.WriteLine($"Fetching coins for chain {chainId}...");

        try
        {
            // Read token list to get addresses
            var tokenListPath = Path.Combine(path, $"tokenlist_{chainId}.json");
            if (!File.Exists(tokenListPath))
            {
                Console.WriteLine($"  Token list not found for chain {chainId}");
                continue;
            }

            var tokenJson = await File.ReadAllTextAsync(tokenListPath);
            var tokens = JsonSerializer.Deserialize<List<TokenMinimal>>(tokenJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokens == null || tokens.Count == 0) continue;

            var addresses = tokens.Select(t => t.Address?.ToLowerInvariant()).Where(a => !string.IsNullOrEmpty(a)).ToList();

            var mapping = await coinGecko.FindCoinGeckoIdsAsync(addresses!, chainId);

            var filePath = Path.Combine(coingeckoPath, $"coins_{chainId}.json");
            var json = JsonSerializer.Serialize(mapping, options);
            await File.WriteAllTextAsync(filePath, json);

            Console.WriteLine($"  Saved {mapping.Count} mappings to coins_{chainId}.json");

            // Rate limit
            await Task.Delay(2000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error: {ex.Message}");
        }
    }

    Console.WriteLine("Done!");
}

async Task RefreshPlatforms(string path)
{
    var coingeckoPath = Path.Combine(path, "coingecko");
    Directory.CreateDirectory(coingeckoPath);

    var coinGecko = new CoinGeckoApiService();
    var options = new JsonSerializerOptions { WriteIndented = false };

    Console.WriteLine("Refreshing CoinGecko asset platforms...");

    try
    {
        var platforms = await coinGecko.GetAssetPlatformsAsync();

        // Filter to platforms with chain identifiers and create mapping
        var mapping = platforms
            .Where(p => p.ChainIdentifier.HasValue)
            .ToDictionary(
                p => p.ChainIdentifier!.Value,
                p => new PlatformInfo
                {
                    Id = p.Id,
                    Name = p.Name,
                    NativeCoinId = p.NativeCoinId
                });

        var filePath = Path.Combine(coingeckoPath, "platforms.json");
        var json = JsonSerializer.Serialize(mapping, options);
        await File.WriteAllTextAsync(filePath, json);

        Console.WriteLine($"Saved {mapping.Count} platforms to platforms.json");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }

    Console.WriteLine("Done!");
}

// Models
class TokenFull
{
    public string? Address { get; set; }
    public string? Symbol { get; set; }
    public string? Name { get; set; }
    public int Decimals { get; set; }
    public string? LogoUri { get; set; }
    public long ChainId { get; set; }
    public string? CoinGeckoId { get; set; }
}

class TokenMinimal
{
    public long ChainId { get; set; }
    public string? Address { get; set; }
    public string? Name { get; set; }
    public string? Symbol { get; set; }
    public int Decimals { get; set; }
}

class PlatformInfo
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? NativeCoinId { get; set; }
}
