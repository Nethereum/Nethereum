using Nethereum.AccountAbstraction.Example.Blazor.Components;
using Nethereum.AccountAbstraction.Example.Core;
using Nethereum.AccountAbstraction.Example.Hosting;
using Nethereum.WebAuthn.Blazor;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddNethereumWebAuthnBlazor();

builder.Services.AddExampleHostDeferred();

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
