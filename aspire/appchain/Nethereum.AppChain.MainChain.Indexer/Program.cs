using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainStorage.Processors.Postgres;
using Nethereum.BlockchainStorage.Token.Postgres;
using Nethereum.BlockchainStore.Postgres;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var mainchainUrl = builder.Configuration["services:mainchain:http:0"]
    ?? builder.Configuration["BlockchainProcessing:BlockchainUrl"]
    ?? "http://localhost:8545";

builder.Configuration["BlockchainProcessing:BlockchainUrl"] = mainchainUrl;
builder.Configuration["BlockchainProcessing:MinimumBlockConfirmations"] = "0";
builder.Configuration["TokenBalanceAggregation:RpcUrl"] = mainchainUrl;

var connectionString = builder.Configuration.GetConnectionString("mainchaindb")
    ?? builder.Configuration.GetConnectionString("PostgresConnection")
    ?? builder.Configuration.GetConnectionString("BlockchainDbStorage");

using (var blockchainContext = new PostgresBlockchainDbContext(connectionString))
{
    await blockchainContext.Database.MigrateAsync();
}

var tokenDbOptions = new DbContextOptionsBuilder<TokenPostgresDbContext>();
tokenDbOptions.UseNpgsql(connectionString).UseLowerCaseNamingConvention();
using (var tokenContext = new TokenPostgresDbContext(tokenDbOptions.Options))
{
    try { await tokenContext.Database.MigrateAsync(); }
    catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P07") { }
}

builder.Services.AddPostgresBlockchainProcessor(builder.Configuration, connectionString);
builder.Services.AddPostgresInternalTransactionProcessor();
builder.Services.AddTokenDenormalizerProcessing(builder.Configuration, connectionString);
builder.Services.AddTokenBalanceAggregationProcessing(builder.Configuration, connectionString);

builder.Build().Run();
