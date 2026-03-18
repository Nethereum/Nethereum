using Microsoft.AspNetCore.Components.Authorization;
using Nethereum.Blazor;
using Nethereum.Blazor.EIP6963WalletInterop;
using Nethereum.Blazor.Storage;
using Nethereum.EIP6963WalletInterop;
using Nethereum.UI;
using NethereumDapp.WebApp.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var devchainUrl = builder.Configuration["services:devchain:http:0"]
    ?? builder.Configuration["services:devchain:https:0"]
    ?? builder.Configuration["Web3:RpcUrl"]
    ?? "http://localhost:8545";

builder.Configuration["Web3:RpcUrl"] = devchainUrl;

builder.Services.AddScoped<IEIP6963WalletInterop, EIP6963WalletBlazorInterop>();
builder.Services.AddScoped<EIP6963WalletHostProvider>();
builder.Services.AddScoped<LocalStorageHelper>();

builder.Services.AddScoped<SelectedEthereumHostProviderService>(sp =>
{
    var walletHostProvider = sp.GetRequiredService<EIP6963WalletHostProvider>();
    var selectedHostProvider = new SelectedEthereumHostProviderService();
    selectedHostProvider.SetSelectedEthereumHostProvider(walletHostProvider);
    return selectedHostProvider;
});

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, EthereumAuthenticationStateProvider>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(EIP6963WalletBlazorInterop).Assembly);

app.MapDefaultEndpoints();

app.Run();
