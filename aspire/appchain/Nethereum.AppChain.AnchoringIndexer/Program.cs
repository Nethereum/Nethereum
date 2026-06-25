using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Nethereum.AppChain.Anchoring.Postgres;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var mainchainUrl = builder.Configuration["services:mainchain:http:0"]
    ?? builder.Configuration["AnchorIndexing:RpcUrl"]
    ?? "http://localhost:53500";

var connectionString = builder.Configuration.GetConnectionString("mainchaindb")
    ?? builder.Configuration.GetConnectionString("PostgresConnection");

if (string.IsNullOrEmpty(connectionString))
{
    Console.Error.WriteLine("No connection string for mainchaindb");
    return;
}

using (var ctx = new AnchorIndexDbContext(
    new DbContextOptionsBuilder<AnchorIndexDbContext>()
        .UseNpgsql(connectionString).UseLowerCaseNamingConvention().Options))
{
    try
    {
        var creator = (Microsoft.EntityFrameworkCore.Storage.RelationalDatabaseCreator)
            ctx.Database.GetService<Microsoft.EntityFrameworkCore.Storage.IDatabaseCreator>();
        await creator.CreateTablesAsync();
    }
    catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P07")
    {
        // Tables already exist — safe to ignore
    }
}

var anchorAddress = "";
using var httpClient = new HttpClient();
for (int attempt = 0; attempt < 30; attempt++)
{
    try
    {
        await Task.Delay(3000);
        var json = await httpClient.GetStringAsync($"{mainchainUrl}/contracts");
        var contracts = JsonSerializer.Deserialize<JsonElement>(json);
        anchorAddress = contracts.GetProperty("anchor").GetString();
        if (!string.IsNullOrEmpty(anchorAddress)) break;
    }
    catch
    {
        Console.WriteLine($"Waiting for mainchain contracts (attempt {attempt + 1})...");
    }
}

if (string.IsNullOrEmpty(anchorAddress))
{
    Console.Error.WriteLine("Failed to get anchor contract address from mainchain");
    return;
}

Console.WriteLine($"Anchor contract: {anchorAddress}");

builder.Configuration["AnchorIndexing:RpcUrl"] = mainchainUrl;
builder.Configuration["AnchorIndexing:AnchorContractAddress"] = anchorAddress;

builder.Services.AddAnchorIndexProcessing(builder.Configuration, connectionString);
builder.Services.AddAnchorSummaryDenormalizerProcessing(builder.Configuration, connectionString);

builder.Build().Run();
