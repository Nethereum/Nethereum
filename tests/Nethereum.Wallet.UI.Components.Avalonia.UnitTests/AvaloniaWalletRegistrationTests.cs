using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Documentation;
using Nethereum.Wallet;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI.Components.Avalonia.Extensions;
using Nethereum.Wallet.UI.Components.Avalonia.Options;
using Nethereum.Wallet.UI.Components.Avalonia.Services;
using Nethereum.Wallet.UI.Components.Avalonia.ViewModels;
using Xunit;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests
{
    public class AvaloniaWalletRegistrationTests
    {
        [Fact]
        [NethereumDocExample(DocSection.WalletUI, "avalonia-wallet-components", "Register the Avalonia wallet components", Order = 1)]
        public void Given_TheAvaloniaWalletComponents_When_TheyAreRegistered_Then_TheOptionsAndPlatformServicesResolve()
        {
            var services = new ServiceCollection();
            services.AddNethereumWalletAvaloniaComponents(options => options.DefaultChainId = 10);
            using var provider = services.BuildServiceProvider();

            var options = provider.GetRequiredService<AvaloniaWalletComponentOptions>();
            var vaultService = provider.GetRequiredService<IWalletVaultService>();
            var storageService = provider.GetRequiredService<IWalletStorageService>();

            Assert.Equal(10, options.DefaultChainId);
            Assert.IsType<AvaloniaWalletVaultService>(vaultService);
            Assert.IsType<AvaloniaWalletStorageService>(storageService);
            Assert.Contains(services, d => d.ServiceType == typeof(WalletHostViewModel));
        }

        [Fact]
        [NethereumDocExample(DocSection.WalletUI, "avalonia-wallet-components", "A service registered first replaces the default", Order = 2)]
        public void Given_AVaultServiceRegisteredFirst_When_TheAvaloniaComponentsAreRegistered_Then_TheExistingRegistrationWins()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IWalletVaultService, CustomVaultService>();
            services.AddNethereumWalletAvaloniaComponents();
            using var provider = services.BuildServiceProvider();

            Assert.IsType<CustomVaultService>(provider.GetRequiredService<IWalletVaultService>());
            Assert.Equal(1, provider.GetRequiredService<AvaloniaWalletComponentOptions>().DefaultChainId);
        }

        private sealed class CustomVaultService : WalletVaultServiceBase
        {
            public override Task<bool> VaultExistsAsync() => Task.FromResult(false);
            protected override Task<string?> GetEncryptedAsync() => Task.FromResult<string?>(null);
            protected override Task SaveEncryptedAsync(string encrypted) => Task.CompletedTask;
            protected override Task ResetStorageAsync() => Task.CompletedTask;
        }
    }
}
