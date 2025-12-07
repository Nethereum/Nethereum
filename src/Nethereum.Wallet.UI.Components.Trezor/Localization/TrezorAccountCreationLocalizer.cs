using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Trezor.ViewModels;

namespace Nethereum.Wallet.UI.Components.Trezor.Localization;

public class TrezorAccountCreationLocalizer : ComponentLocalizerBase<TrezorAccountCreationViewModel>
{
    public static class Keys
    {
        public const string DisplayName = "DisplayName";
        public const string Description = "Description";
        public const string WalletNameLabel = "WalletNameLabel";
        public const string WalletNamePlaceholder = "WalletNamePlaceholder";
        public const string WalletNameHelper = "WalletNameHelper";
        public const string ConnectInstruction = "ConnectInstruction";
        public const string ScanButtonText = "ScanButtonText";
        public const string StepConnectLabel = "StepConnectLabel";
        public const string StepSelectLabel = "StepSelectLabel";
        public const string StepConfirmLabel = "StepConfirmLabel";
        public const string AccountPreviewTitle = "AccountPreviewTitle";
        public const string AccountPreviewDescription = "AccountPreviewDescription";
        public const string AccountLabelField = "AccountLabelField";
        public const string AccountLabelHelper = "AccountLabelHelper";
        public const string SelectedAddressLabel = "SelectedAddressLabel";
        public const string ConfirmSummaryTitle = "ConfirmSummaryTitle";
        public const string ConfirmSummaryDescription = "ConfirmSummaryDescription";
        public const string ExitButtonText = "ExitButtonText";
        public const string BackButtonText = "BackButtonText";
        public const string ContinueButtonText = "ContinueButtonText";
        public const string CreateAccountButtonText = "CreateAccountButtonText";
        public const string DeviceSummaryLabel = "DeviceSummaryLabel";
        public const string IndexSummaryLabel = "IndexSummaryLabel";
        public const string SelectedAddressPlaceholder = "SelectedAddressPlaceholder";
        public const string NoAddressesDiscovered = "NoAddressesDiscovered";
        public const string ScanSuccessMessage = "ScanSuccessMessage";
        public const string ScanningProgressMessage = "ScanningProgressMessage";
        public const string StartIndexLabel = "StartIndexLabel";
        public const string SingleIndexLabel = "SingleIndexLabel";
        public const string LoadIndexButtonText = "LoadIndexButtonText";
    }

    public TrezorAccountCreationLocalizer(IWalletLocalizationService globalService) : base(globalService)
    {
    }

    protected override void RegisterTranslations()
    {
        _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
        {
            [Keys.DisplayName] = "Trezor Hardware Wallet",
            [Keys.Description] = "Connect your Trezor device and select the account you want to use.",
            [Keys.WalletNameLabel] = "Wallet name",
            [Keys.WalletNamePlaceholder] = "My Trezor",
            [Keys.WalletNameHelper] = "Choose a name to identify this hardware wallet.",
            [Keys.ConnectInstruction] = "Plug in your Trezor, unlock it, and scan for addresses.",
            [Keys.ScanButtonText] = "Scan Addresses",
            [Keys.StepConnectLabel] = "Connect",
            [Keys.StepSelectLabel] = "Select",
            [Keys.StepConfirmLabel] = "Confirm",
            [Keys.AccountPreviewTitle] = "Available Addresses",
            [Keys.AccountPreviewDescription] = "Choose which derived address you want to add to your wallet.",
            [Keys.AccountLabelField] = "Account Label",
            [Keys.AccountLabelHelper] = "Optional label that will appear in your wallet.",
            [Keys.SelectedAddressLabel] = "Selected Address",
            [Keys.ConfirmSummaryTitle] = "Summary",
            [Keys.ConfirmSummaryDescription] = "Review the address and label before creating the account.",
            [Keys.ExitButtonText] = "Cancel",
            [Keys.BackButtonText] = "Back",
            [Keys.ContinueButtonText] = "Continue",
            [Keys.CreateAccountButtonText] = "Add Account",
            [Keys.DeviceSummaryLabel] = "Device",
            [Keys.IndexSummaryLabel] = "Index",
            [Keys.SelectedAddressPlaceholder] = "Select an address to continue.",
            [Keys.NoAddressesDiscovered] = "No addresses were found. Make sure your Trezor is unlocked and try scanning again.",
            [Keys.ScanSuccessMessage] = "Addresses loaded successfully. Continue to select one.",
            [Keys.ScanningProgressMessage] = "Waiting for your Trezor to respond...",
            [Keys.StartIndexLabel] = "Start index",
            [Keys.SingleIndexLabel] = "Address index",
            [Keys.LoadIndexButtonText] = "Load index"
        });

        _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
        {
            [Keys.DisplayName] = "Cartera Trezor",
            [Keys.Description] = "Conecta tu dispositivo Trezor y selecciona la cuenta que deseas usar.",
            [Keys.WalletNameLabel] = "Nombre de la cartera",
            [Keys.WalletNamePlaceholder] = "Mi Trezor",
            [Keys.WalletNameHelper] = "Elige un nombre para identificar esta cartera de hardware.",
            [Keys.ConnectInstruction] = "Conecta tu Trezor, desbloquéalo y pulsa Escanear para ver las direcciones.",
            [Keys.ScanButtonText] = "Escanear direcciones",
            [Keys.StepConnectLabel] = "Conectar",
            [Keys.StepSelectLabel] = "Seleccionar",
            [Keys.StepConfirmLabel] = "Confirmar",
            [Keys.AccountPreviewTitle] = "Direcciones disponibles",
            [Keys.AccountPreviewDescription] = "Elige qué dirección derivada deseas agregar a tu cartera.",
            [Keys.AccountLabelField] = "Etiqueta de cuenta",
            [Keys.AccountLabelHelper] = "Etiqueta opcional que se mostrará en tu cartera.",
            [Keys.SelectedAddressLabel] = "Dirección seleccionada",
            [Keys.ConfirmSummaryTitle] = "Resumen",
            [Keys.ConfirmSummaryDescription] = "Revisa la dirección y etiqueta antes de crear la cuenta.",
            [Keys.ExitButtonText] = "Cancelar",
            [Keys.BackButtonText] = "Atrás",
            [Keys.ContinueButtonText] = "Continuar",
            [Keys.CreateAccountButtonText] = "Agregar cuenta",
            [Keys.DeviceSummaryLabel] = "Dispositivo",
            [Keys.IndexSummaryLabel] = "Índice",
            [Keys.SelectedAddressPlaceholder] = "Selecciona una dirección para continuar.",
            [Keys.NoAddressesDiscovered] = "No se encontraron direcciones. Asegúrate de que tu Trezor esté desbloqueado e inténtalo nuevamente.",
            [Keys.ScanSuccessMessage] = "Direcciones cargadas correctamente. Continúa para seleccionar una.",
            [Keys.ScanningProgressMessage] = "Esperando la respuesta de tu Trezor...",
            [Keys.StartIndexLabel] = "Índice inicial",
            [Keys.SingleIndexLabel] = "Índice de dirección",
            [Keys.LoadIndexButtonText] = "Cargar índice"
        });
    }
}
