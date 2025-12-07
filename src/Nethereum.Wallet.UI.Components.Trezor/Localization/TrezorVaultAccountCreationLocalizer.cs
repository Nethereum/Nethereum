using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Trezor.ViewModels;

namespace Nethereum.Wallet.UI.Components.Trezor.Localization;

public class TrezorVaultAccountCreationLocalizer : ComponentLocalizerBase<TrezorVaultAccountCreationViewModel>
{
    public static class Keys
    {
        public const string DisplayName = "DisplayName";
        public const string Description = "Description";
        public const string StepSelectDeviceLabel = "StepSelectDeviceLabel";
        public const string StepDiscoverLabel = "StepDiscoverLabel";
        public const string StepConfirmLabel = "StepConfirmLabel";
        public const string SelectDeviceTitle = "SelectDeviceTitle";
        public const string SelectDeviceDescription = "SelectDeviceDescription";
        public const string SelectDeviceLabel = "SelectDeviceLabel";
        public const string DeviceRequired = "DeviceRequired";
        public const string NoDevicesTitle = "NoDevicesTitle";
        public const string NoDevicesDescription = "NoDevicesDescription";
        public const string StartIndexLabel = "StartIndexLabel";
        public const string SingleIndexLabel = "SingleIndexLabel";
        public const string ScanButtonText = "ScanButtonText";
        public const string LoadIndexButtonText = "LoadIndexButtonText";
        public const string ScanningProgressMessage = "ScanningProgressMessage";
        public const string ScanSuccessMessage = "ScanSuccessMessage";
        public const string SelectedAddressLabel = "SelectedAddressLabel";
        public const string AccountLabelField = "AccountLabelField";
        public const string AccountLabelHelper = "AccountLabelHelper";
        public const string ConfirmSummaryTitle = "ConfirmSummaryTitle";
        public const string ConfirmSummaryDescription = "ConfirmSummaryDescription";
        public const string DeviceSummaryLabel = "DeviceSummaryLabel";
        public const string IndexSummaryLabel = "IndexSummaryLabel";
        public const string ExitButtonText = "ExitButtonText";
        public const string BackButtonText = "BackButtonText";
        public const string ContinueButtonText = "ContinueButtonText";
        public const string CreateAccountButtonText = "CreateAccountButtonText";
    }

    public TrezorVaultAccountCreationLocalizer(IWalletLocalizationService globalService) : base(globalService)
    {
    }

    protected override void RegisterTranslations()
    {
        _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
        {
            [Keys.DisplayName] = "Add Account from Trezor",
            [Keys.Description] = "Use a connected Trezor device that already exists in your wallet to derive another account.",
            [Keys.StepSelectDeviceLabel] = "Device",
            [Keys.StepDiscoverLabel] = "Discover",
            [Keys.StepConfirmLabel] = "Confirm",
            [Keys.SelectDeviceTitle] = "Select Trezor Device",
            [Keys.SelectDeviceDescription] = "Choose which saved device you want to derive the new address from.",
            [Keys.SelectDeviceLabel] = "Device",
            [Keys.DeviceRequired] = "Select a Trezor device to continue.",
            [Keys.NoDevicesTitle] = "No Trezor devices available",
            [Keys.NoDevicesDescription] = "First add a Trezor account to your wallet. Once a device is registered, you can derive more addresses from it here.",
            [Keys.StartIndexLabel] = "Start index",
            [Keys.SingleIndexLabel] = "Address index",
            [Keys.ScanButtonText] = "Scan addresses",
            [Keys.LoadIndexButtonText] = "Load index",
            [Keys.ScanningProgressMessage] = "Waiting for your Trezor to respond...",
            [Keys.ScanSuccessMessage] = "Addresses loaded successfully. Select one to continue.",
            [Keys.SelectedAddressLabel] = "Selected address",
            [Keys.AccountLabelField] = "Account label",
            [Keys.AccountLabelHelper] = "Optional label to help you recognize this address.",
            [Keys.ConfirmSummaryTitle] = "Summary",
            [Keys.ConfirmSummaryDescription] = "Review the details before creating the account.",
            [Keys.DeviceSummaryLabel] = "Device",
            [Keys.IndexSummaryLabel] = "Index",
            [Keys.ExitButtonText] = "Cancel",
            [Keys.BackButtonText] = "Back",
            [Keys.ContinueButtonText] = "Continue",
            [Keys.CreateAccountButtonText] = "Add account"
        });

        _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
        {
            [Keys.DisplayName] = "Agregar cuenta desde Trezor",
            [Keys.Description] = "Usa un dispositivo Trezor ya registrado en tu cartera para derivar otra cuenta.",
            [Keys.StepSelectDeviceLabel] = "Dispositivo",
            [Keys.StepDiscoverLabel] = "Descubrir",
            [Keys.StepConfirmLabel] = "Confirmar",
            [Keys.SelectDeviceTitle] = "Selecciona el dispositivo Trezor",
            [Keys.SelectDeviceDescription] = "Elige el dispositivo guardado del que deseas derivar la nueva dirección.",
            [Keys.SelectDeviceLabel] = "Dispositivo",
            [Keys.DeviceRequired] = "Selecciona un dispositivo Trezor para continuar.",
            [Keys.NoDevicesTitle] = "No hay dispositivos Trezor disponibles",
            [Keys.NoDevicesDescription] = "Primero agrega una cuenta Trezor a tu cartera. Luego podrás derivar más direcciones desde aquí.",
            [Keys.StartIndexLabel] = "Índice inicial",
            [Keys.SingleIndexLabel] = "Índice de dirección",
            [Keys.ScanButtonText] = "Escanear direcciones",
            [Keys.LoadIndexButtonText] = "Cargar índice",
            [Keys.ScanningProgressMessage] = "Esperando la respuesta de tu Trezor...",
            [Keys.ScanSuccessMessage] = "Direcciones cargadas correctamente. Selecciona una para continuar.",
            [Keys.SelectedAddressLabel] = "Dirección seleccionada",
            [Keys.AccountLabelField] = "Etiqueta de la cuenta",
            [Keys.AccountLabelHelper] = "Etiqueta opcional para reconocer esta dirección.",
            [Keys.ConfirmSummaryTitle] = "Resumen",
            [Keys.ConfirmSummaryDescription] = "Revisa los detalles antes de crear la cuenta.",
            [Keys.DeviceSummaryLabel] = "Dispositivo",
            [Keys.IndexSummaryLabel] = "Índice",
            [Keys.ExitButtonText] = "Cancelar",
            [Keys.BackButtonText] = "Atrás",
            [Keys.ContinueButtonText] = "Continuar",
            [Keys.CreateAccountButtonText] = "Agregar cuenta"
        });
    }
}
