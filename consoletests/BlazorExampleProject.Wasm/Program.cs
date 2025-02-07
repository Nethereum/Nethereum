using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethereum.UI;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Components.Authorization;
using Nethereum.Blazor;
using Nethereum.EIP6963WalletInterop;
using Nethereum.Blazor.EIP6963WalletInterop;
using Nethereum.Blazor.Storage;

namespace ExampleProject.Wasm
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.Services.AddAuthorizationCore();

            builder.Services.AddSingleton(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddSingleton<IEIP6963WalletInterop, EIP6963WalletBlazorInterop>();
            builder.Services.AddSingleton<EIP6963WalletHostProvider>();
            builder.Services.AddSingleton<LocalStorageHelper>();

            //Add eip as the selected ethereum host provider
            builder.Services.AddSingleton(services =>
            {
                var walletHostProvider = services.GetService<EIP6963WalletHostProvider>();
                var selectedHostProvider = new SelectedEthereumHostProviderService();
                selectedHostProvider.SetSelectedEthereumHostProvider(walletHostProvider);
                return selectedHostProvider;
            });
               
            
            builder.Services.AddSingleton<AuthenticationStateProvider, EthereumAuthenticationStateProvider>();

           

            await builder.Build().RunAsync();
        }
    }
}
