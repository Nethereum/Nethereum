using Nethereum.Explorer.Anchoring;
using Nethereum.Explorer.Services;
using Nethereum.BlockchainStorage.Token.Postgres;
using Nethereum.BlockchainStore.Postgres;
using Nethereum.Mud.Repositories.EntityFramework;
using Nethereum.Mud.Repositories.Postgres;
using Nethereum.Mud.Repositories.Postgres.StoreRecordsNormaliser;
using Nethereum.Mud.TableRepository;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("mainchaindb")
    ?? builder.Configuration.GetConnectionString("BlockchainDbStorage");

if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddPostgresBlockchainStorage(connectionString);

    builder.Services.AddDbContext<MudPostgresStoreRecordsDbContext>(options =>
        options.UseNpgsql(connectionString)
            .UseLowerCaseNamingConvention());

    builder.Services.AddTokenPostgresRepositories(connectionString);

    builder.Services.AddScoped<IMudStoreRecordsDbSets>(sp =>
        sp.GetRequiredService<MudPostgresStoreRecordsDbContext>());

    builder.Services.AddTransient<INormalisedTableQueryService>(sp =>
    {
        var conn = new NpgsqlConnection(connectionString);
        var logger = sp.GetService<ILogger<NormalisedTableQueryService>>();
        return new NormalisedTableQueryService(conn, logger);
    });
}

var mainchainUrl = builder.Configuration["services:mainchain:http:0"]
    ?? builder.Configuration["services:mainchain:https:0"]
    ?? builder.Configuration["Explorer:RpcUrl"];

if (!string.IsNullOrEmpty(mainchainUrl))
{
    builder.Configuration["Explorer:RpcUrl"] = mainchainUrl;
}

builder.Services.AddExplorerServices(builder.Configuration);
builder.Services.AddAnchorExplorerServices(connectionString);

builder.Services.AddHttpClient("anchoring", client =>
{
    client.BaseAddress = new Uri("https+http://anchoring");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAntiforgery();
app.UseStaticFiles();
app.MapStaticAssets();
app.MapRazorComponents<Nethereum.AppChain.MainChain.Explorer.Components.App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(Nethereum.Explorer.Components.App).Assembly,
        typeof(Nethereum.Blazor.EIP6963WalletInterop.EIP6963WalletBlazorInterop).Assembly,
        typeof(Nethereum.Explorer.Anchoring.Services.IAnchorExplorerService).Assembly);

app.MapTokenApiEndpoints();
app.MapContractApiEndpoints();
app.MapDefaultEndpoints();

app.Run();
