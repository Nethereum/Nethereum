using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Nethereum.Reown.AppKit.Blazor;
using NethereumReownAppKitBlazor;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");


var projectId = "";
if (string.IsNullOrEmpty(projectId)) {
	throw new InvalidOperationException("Set your Reown project ID from https://cloud.reown.com/sign-in");
}

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMudServices();
builder.Services.AddAppKit(new() {
	Networks = NetworkConstants.Networks.All,
	ProjectId = projectId,
	Name = "AppKit Example",
	Description = "An example project to showcase Reown AppKit",
	Url = builder.HostEnvironment.BaseAddress,
	Icons = ["https://reown.com/favicon.ico"],
	Swaps = true,
	Onramp = true,
	History = true,
	Debug = true,
	ThemeMode = ThemeModeOptions.light,
});

await builder.Build().RunAsync();
