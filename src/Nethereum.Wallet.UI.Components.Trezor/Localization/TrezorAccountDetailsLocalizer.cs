using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Trezor.ViewModels;

namespace Nethereum.Wallet.UI.Components.Trezor.Localization;

public class TrezorAccountDetailsLocalizer : ComponentLocalizerBase<TrezorAccountDetailsViewModel>
{
    public static class Keys
    {
        public const string Title = "Title";
        public const string Subtitle = "Subtitle";
        public const string DeviceIdLabel = "DeviceIdLabel";
        public const string IndexLabel = "IndexLabel";
        public const string AddressLabel = "AddressLabel";
        public const string AccountNameLabel = "AccountNameLabel";
        public const string AccountNamePlaceholder = "AccountNamePlaceholder";
        public const string AccountNameHelper = "AccountNameHelper";
        public const string AccountNameRequired = "AccountNameRequired";
        public const string AccountNameUpdated = "AccountNameUpdated";
        public const string AccountNameUpdateFailed = "AccountNameUpdateFailed";
        public const string RemoveAccountButton = "RemoveAccountButton";
        public const string CancelButton = "CancelButton";
        public const string ConfirmRemovalTitle = "ConfirmRemovalTitle";
        public const string ConfirmRemovalMessage = "ConfirmRemovalMessage";
        public const string CannotRemoveLastAccount = "CannotRemoveLastAccount";
        public const string AccountRemoved = "AccountRemoved";
        public const string RemoveAccountFailed = "RemoveAccountFailed";
        public const string DeviceSummaryLabel = "DeviceSummaryLabel";
    }

    public TrezorAccountDetailsLocalizer(IWalletLocalizationService globalService)
        : base(globalService)
    {
    }

    protected override void RegisterTranslations()
    {
        _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
        {
            [Keys.Title] = "Trezor Account",
            [Keys.Subtitle] = "Manage your hardware-backed account",
            [Keys.DeviceIdLabel] = "Device ID",
            [Keys.IndexLabel] = "Derivation index",
            [Keys.AddressLabel] = "Address",
            [Keys.AccountNameLabel] = "Account label",
            [Keys.AccountNamePlaceholder] = "Friendly name",
            [Keys.AccountNameHelper] = "Helps you recognize this address across the app.",
            [Keys.AccountNameRequired] = "Account name cannot be empty.",
            [Keys.AccountNameUpdated] = "Account name updated.",
            [Keys.AccountNameUpdateFailed] = "Unable to update account name: {0}",
            [Keys.RemoveAccountButton] = "Remove account",
            [Keys.CancelButton] = "Cancel",
            [Keys.ConfirmRemovalTitle] = "Remove Trezor account?",
            [Keys.ConfirmRemovalMessage] = "This will remove the account from this device list but will not delete it from your Trezor.",
            [Keys.CannotRemoveLastAccount] = "You must keep at least one account in your wallet.",
            [Keys.AccountRemoved] = "Account removed.",
            [Keys.RemoveAccountFailed] = "Failed to remove account: {0}",
            [Keys.DeviceSummaryLabel] = "Device"
        });

        _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
        {
            [Keys.Title] = "Cuenta Trezor",
            [Keys.Subtitle] = "Gestiona tu cuenta protegida por hardware",
            [Keys.DeviceIdLabel] = "ID del dispositivo",
            [Keys.IndexLabel] = "Índice de derivación",
            [Keys.AddressLabel] = "Dirección",
            [Keys.AccountNameLabel] = "Etiqueta de la cuenta",
            [Keys.AccountNamePlaceholder] = "Nombre amigable",
            [Keys.AccountNameHelper] = "Te ayuda a reconocer esta dirección en la aplicación.",
            [Keys.AccountNameRequired] = "El nombre de la cuenta no puede estar vacío.",
            [Keys.AccountNameUpdated] = "Nombre de la cuenta actualizado.",
            [Keys.AccountNameUpdateFailed] = "No se pudo actualizar el nombre: {0}",
            [Keys.RemoveAccountButton] = "Eliminar cuenta",
            [Keys.CancelButton] = "Cancelar",
            [Keys.ConfirmRemovalTitle] = "¿Eliminar cuenta Trezor?",
            [Keys.ConfirmRemovalMessage] = "Esto quitará la cuenta de la lista pero no la borrará de tu Trezor.",
            [Keys.CannotRemoveLastAccount] = "Debes mantener al menos una cuenta en tu cartera.",
            [Keys.AccountRemoved] = "Cuenta eliminada.",
            [Keys.RemoveAccountFailed] = "No se pudo eliminar la cuenta: {0}",
            [Keys.DeviceSummaryLabel] = "Dispositivo"
        });
    }
}
