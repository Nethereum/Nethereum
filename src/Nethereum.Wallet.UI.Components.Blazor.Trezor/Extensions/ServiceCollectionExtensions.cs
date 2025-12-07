#nullable enable

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nethereum.Signer.Trezor.Abstractions;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.AccountDetails;
using Nethereum.Wallet.UI.Components.WalletAccounts;
using Nethereum.Wallet.UI.Components.Trezor.ViewModels;
using Nethereum.Wallet.UI.Components.Blazor.Trezor.Prompts;
using Nethereum.Wallet.UI.Components.Trezor;
using Nethereum.Wallet.UI.Components.Trezor.Localization;

namespace Nethereum.Wallet.UI.Components.Blazor.Trezor.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTrezorWalletBlazorComponents(this IServiceCollection services)
    {
        services.AddSingleton<IAccountMetadataViewModel, TrezorAccountMetadataProvider>();
        services.AddTransient<TrezorAccountCreationViewModel>();
        services.AddTransient<IAccountCreationViewModel>(sp => sp.GetRequiredService<TrezorAccountCreationViewModel>());
        services.AddTransient<TrezorVaultAccountCreationViewModel>();
        services.AddTransient<IAccountCreationViewModel>(sp => sp.GetRequiredService<TrezorVaultAccountCreationViewModel>());
        services.AddTransient<TrezorAccountDetailsViewModel>();
        services.AddTransient<IAccountDetailsViewModel, TrezorAccountDetailsViewModel>();
        services.AddTransient<TrezorGroupDetailsViewModel>();
        services.AddTransient<IGroupDetailsViewModel, TrezorGroupDetailsViewModel>();

        services.TryAddSingleton<IComponentLocalizer<TrezorAccountCreationViewModel>, TrezorAccountCreationLocalizer>();
        services.TryAddSingleton<IComponentLocalizer<TrezorVaultAccountCreationViewModel>, TrezorVaultAccountCreationLocalizer>();
        services.TryAddSingleton<IComponentLocalizer<TrezorPinPrompt>, TrezorPinPromptLocalizer>();
        services.TryAddSingleton<IComponentLocalizer<TrezorPassphrasePrompt>, TrezorPassphrasePromptLocalizer>();
        services.TryAddSingleton<IComponentLocalizer<TrezorAccountDetailsViewModel>, TrezorAccountDetailsLocalizer>();
        services.TryAddSingleton<IComponentLocalizer<TrezorGroupDetailsViewModel>, TrezorGroupDetailsLocalizer>();

        services.AddTransient<BlazorTrezorPromptHandler>();
        services.AddTransient<ITrezorPromptHandler, BlazorTrezorPromptHandler>();

        services.AddSingleton<IWalletUIRegistryContributor, TrezorWalletUIRegistryContributor>();

        return services;
    }
}
