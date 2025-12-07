using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Trezor.ViewModels;

namespace Nethereum.Wallet.UI.Components.Trezor.Localization;

public class TrezorGroupDetailsLocalizer : ComponentLocalizerBase<TrezorGroupDetailsViewModel>
{
    public static class Keys
    {
        public const string Title = "Title";
        public const string Subtitle = "Subtitle";
        public const string DeviceIdLabel = "DeviceIdLabel";
        public const string AccountListTitle = "AccountListTitle";
        public const string AddAccountSubtitle = "AddAccountSubtitle";
        public const string AddAccountButton = "AddAccountButton";
        public const string NextIndexLabel = "NextIndexLabel";
        public const string DefaultAccountLabel = "DefaultAccountLabel";
        public const string AccountAddedSuccess = "AccountAddedSuccess";
        public const string AccountAddedFailed = "AccountAddedFailed";
        public const string RefreshButton = "RefreshButton";
        public const string EmptyStateTitle = "EmptyStateTitle";
        public const string EmptyStateDescription = "EmptyStateDescription";
        public const string DeviceLabelField = "DeviceLabelField";
        public const string DeviceLabelHelper = "DeviceLabelHelper";
        public const string RenameButton = "RenameButton";
        public const string SaveDeviceLabelButton = "SaveDeviceLabelButton";
        public const string CancelEditButton = "CancelEditButton";
        public const string DeviceLabelSaved = "DeviceLabelSaved";
        public const string DeviceLabelSaveFailed = "DeviceLabelSaveFailed";
        public const string DeviceOverviewTitle = "DeviceOverviewTitle";
        public const string AccountCountLabel = "AccountCountLabel";
        public const string HardwareTypeLabel = "HardwareTypeLabel";
        public const string DefaultDeviceLabel = "DefaultDeviceLabel";
    }

    public TrezorGroupDetailsLocalizer(IWalletLocalizationService globalService)
        : base(globalService)
    {
    }

    protected override void RegisterTranslations()
    {
        _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
        {
            [Keys.Title] = "Trezor device",
            [Keys.Subtitle] = "View and manage addresses derived from this device.",
            [Keys.DeviceIdLabel] = "Device ID",
            [Keys.AccountListTitle] = "Linked addresses",
            [Keys.AddAccountSubtitle] = "Add the next derivation index to this wallet.",
            [Keys.AddAccountButton] = "Add next address",
            [Keys.NextIndexLabel] = "Next derivation index",
            [Keys.DefaultAccountLabel] = "Account",
            [Keys.AccountAddedSuccess] = "Address #{0} added.",
            [Keys.AccountAddedFailed] = "Unable to add address: {0}",
            [Keys.RefreshButton] = "Refresh",
            [Keys.EmptyStateTitle] = "No addresses found",
            [Keys.EmptyStateDescription] = "Add your first address from this Trezor device.",
            [Keys.DeviceLabelField] = "Wallet name",
            [Keys.DeviceLabelHelper] = "This label is shown across the wallet when selecting this hardware device.",
            [Keys.RenameButton] = "Rename wallet",
            [Keys.SaveDeviceLabelButton] = "Save name",
            [Keys.CancelEditButton] = "Cancel",
            [Keys.DeviceLabelSaved] = "Wallet name updated.",
            [Keys.DeviceLabelSaveFailed] = "Unable to update wallet name: {0}",
            [Keys.DeviceOverviewTitle] = "Device summary",
            [Keys.AccountCountLabel] = "Accounts",
            [Keys.HardwareTypeLabel] = "Hardware wallet",
            [Keys.DefaultDeviceLabel] = "Trezor wallet"
        });

        _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
        {
            [Keys.Title] = "Dispositivo Trezor",
            [Keys.Subtitle] = "Consulta y gestiona las direcciones derivadas de este dispositivo.",
            [Keys.DeviceIdLabel] = "ID del dispositivo",
            [Keys.AccountListTitle] = "Direcciones vinculadas",
            [Keys.AddAccountSubtitle] = "Agrega el siguiente índice de derivación a esta cartera.",
            [Keys.AddAccountButton] = "Agregar siguiente dirección",
            [Keys.NextIndexLabel] = "Próximo índice",
            [Keys.DefaultAccountLabel] = "Cuenta",
            [Keys.AccountAddedSuccess] = "Dirección #{0} agregada.",
            [Keys.AccountAddedFailed] = "No se pudo agregar la dirección: {0}",
            [Keys.RefreshButton] = "Actualizar",
            [Keys.EmptyStateTitle] = "No se encontraron direcciones",
            [Keys.EmptyStateDescription] = "Agrega tu primera dirección desde este dispositivo Trezor.",
            [Keys.DeviceLabelField] = "Nombre de la cartera",
            [Keys.DeviceLabelHelper] = "Este nombre se mostrará en toda la aplicación cuando selecciones este dispositivo.",
            [Keys.RenameButton] = "Renombrar cartera",
            [Keys.SaveDeviceLabelButton] = "Guardar nombre",
            [Keys.CancelEditButton] = "Cancelar",
            [Keys.DeviceLabelSaved] = "Nombre de la cartera actualizado.",
            [Keys.DeviceLabelSaveFailed] = "No se pudo actualizar el nombre: {0}",
            [Keys.DeviceOverviewTitle] = "Resumen del dispositivo",
            [Keys.AccountCountLabel] = "Cuentas",
            [Keys.HardwareTypeLabel] = "Cartera de hardware",
            [Keys.DefaultDeviceLabel] = "Cartera Trezor"
        });
    }
}
