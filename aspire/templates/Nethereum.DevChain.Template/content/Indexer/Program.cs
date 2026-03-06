using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainStorage.Processors.Postgres;
using Nethereum.BlockchainStorage.Token.Postgres;
using Nethereum.BlockchainStore.Postgres;
using Nethereum.Mud.Repositories.Postgres;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var devchainUrl = builder.Configuration["services:devchain:http:0"]
    ?? builder.Configuration["services:devchain:https:0"]
    ?? builder.Configuration["BlockchainProcessing:BlockchainUrl"]
    ?? "http://localhost:8545";

builder.Configuration["BlockchainProcessing:BlockchainUrl"] = devchainUrl;
builder.Configuration["BlockchainProcessing:MinimumBlockConfirmations"] = "0";
builder.Configuration["MudProcessing:RpcUrl"] = devchainUrl;
builder.Configuration["TokenBalanceAggregation:RpcUrl"] = devchainUrl;

var connectionString = builder.Configuration.GetConnectionString("nethereumdb")
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
    await tokenContext.Database.MigrateAsync();
}

builder.Services.AddPostgresBlockchainProcessor(builder.Configuration, connectionString);
builder.Services.AddPostgresInternalTransactionProcessor();

builder.Services.AddTokenDenormalizerProcessing(builder.Configuration, connectionString);
builder.Services.AddTokenBalanceAggregationProcessing(builder.Configuration, connectionString);

var mudOptions = new DbContextOptionsBuilder<MudPostgresStoreRecordsDbContext>();
mudOptions.UseNpgsql(connectionString).UseLowerCaseNamingConvention();
using (var mudContext = new MudPostgresStoreRecordsDbContext(mudOptions.Options))
{
    var script = mudContext.Database.GenerateCreateScript();
    script = System.Text.RegularExpressions.Regex.Replace(script, @"CREATE\s+TABLE\s+", "CREATE TABLE IF NOT EXISTS ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    script = System.Text.RegularExpressions.Regex.Replace(script, @"CREATE\s+UNIQUE\s+INDEX\s+", "CREATE UNIQUE INDEX IF NOT EXISTS ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    script = System.Text.RegularExpressions.Regex.Replace(script, @"CREATE\s+INDEX\s+", "CREATE INDEX IF NOT EXISTS ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    foreach (var statement in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (string.IsNullOrWhiteSpace(statement)) continue;
        try
        {
            await mudContext.Database.ExecuteSqlRawAsync(statement);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P07")
        {
        }
    }
}

var mudAddress = builder.Configuration["MudProcessing:Address"];
if (!string.IsNullOrWhiteSpace(mudAddress))
{
    builder.Services.AddMudPostgresProcessing(builder.Configuration, connectionString);
    builder.Services.AddMudNormaliserProcessing(builder.Configuration, connectionString);
}
else
{
    builder.Services.AddMudWorldAddressDiscovery(builder.Configuration, connectionString);
}

builder.Build().Run();
