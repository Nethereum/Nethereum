using Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Blazor.Components;
using Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core;
using Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddEnterpriseDemoHostDeferred();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Lifetime.ApplicationStopping.Register(() =>
{
    var session = app.Services.GetRequiredService<SessionState>();
    session.InfraResource?.DisposeAsync().AsTask().GetAwaiter().GetResult();
});

app.Run();
