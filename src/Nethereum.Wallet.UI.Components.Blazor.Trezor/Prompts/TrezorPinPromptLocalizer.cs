using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Blazor.Trezor.Prompts;

public class TrezorPinPromptLocalizer : ComponentLocalizerBase<TrezorPinPrompt>
{
    public static class Keys
    {
        public const string Title = "Title";
        public const string Description = "Description";
        public const string Helper = "Helper";
        public const string PinLabel = "PinLabel";
        public const string PinPlaceholder = "PinPlaceholder";
        public const string PinHelper = "PinHelper";
        public const string ToggleVisibilityLabel = "ToggleVisibilityLabel";
        public const string ClearButtonText = "ClearButtonText";
        public const string CancelButtonText = "CancelButtonText";
        public const string ConfirmButtonText = "ConfirmButtonText";
    }

    public TrezorPinPromptLocalizer(IWalletLocalizationService globalService)
        : base(globalService)
    {
    }

    protected override void RegisterTranslations()
    {
        _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
        {
            [Keys.Title] = "Enter Trezor PIN",
            [Keys.Description] = "Use the PIN layout shown on your Trezor to enter the position digits.",
            [Keys.Helper] = "Each digit corresponds to the position displayed on your device. Never share the visual layout.",
            [Keys.PinLabel] = "PIN",
            [Keys.PinPlaceholder] = "●●●●",
            [Keys.PinHelper] = "Digits 1-9 only. The order must match what you enter on your Trezor.",
            [Keys.ToggleVisibilityLabel] = "Toggle PIN visibility",
            [Keys.ClearButtonText] = "Clear",
            [Keys.CancelButtonText] = "Cancel",
            [Keys.ConfirmButtonText] = "Confirm"
        });

        _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
        {
            [Keys.Title] = "Introduce el PIN de Trezor",
            [Keys.Description] = "Usa la cuadrícula mostrada en tu Trezor para introducir los dígitos de posición.",
            [Keys.Helper] = "Cada dígito corresponde a la posición que ves en el dispositivo. Nunca compartas el diseño visual.",
            [Keys.PinLabel] = "PIN",
            [Keys.PinPlaceholder] = "●●●●",
            [Keys.PinHelper] = "Solo dígitos del 1 al 9. El orden debe coincidir con lo ingresado en tu Trezor.",
            [Keys.ToggleVisibilityLabel] = "Mostrar/ocultar PIN",
            [Keys.ClearButtonText] = "Limpiar",
            [Keys.CancelButtonText] = "Cancelar",
            [Keys.ConfirmButtonText] = "Confirmar"
        });
    }
}
