using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Blazor.Trezor.Prompts;

public class TrezorPassphrasePromptLocalizer : ComponentLocalizerBase<TrezorPassphrasePrompt>
{
    public static class Keys
    {
        public const string Title = "Title";
        public const string Description = "Description";
        public const string PassphraseLabel = "PassphraseLabel";
        public const string PassphrasePlaceholder = "PassphrasePlaceholder";
        public const string PassphraseHelper = "PassphraseHelper";
        public const string PassphraseInfo = "PassphraseInfo";
        public const string ToggleVisibilityLabel = "ToggleVisibilityLabel";
        public const string CancelButtonText = "CancelButtonText";
        public const string ConfirmButtonText = "ConfirmButtonText";
    }

    public TrezorPassphrasePromptLocalizer(IWalletLocalizationService globalService)
        : base(globalService)
    {
    }

    protected override void RegisterTranslations()
    {
        _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
        {
            [Keys.Title] = "Enter Passphrase",
            [Keys.Description] = "If your Trezor wallet uses a passphrase, enter it exactly as configured.",
            [Keys.PassphraseLabel] = "Passphrase",
            [Keys.PassphrasePlaceholder] = "Optional passphrase",
            [Keys.PassphraseHelper] = "Leave blank if you use the standard wallet.",
            [Keys.PassphraseInfo] = "Passphrases are case sensitive. Wrong values create a different hidden wallet.",
            [Keys.ToggleVisibilityLabel] = "Toggle passphrase visibility",
            [Keys.CancelButtonText] = "Cancel",
            [Keys.ConfirmButtonText] = "Continue"
        });

        _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
        {
            [Keys.Title] = "Introduce la frase secreta",
            [Keys.Description] = "Si tu Trezor usa una frase secreta, introdúcela exactamente como la configuraste.",
            [Keys.PassphraseLabel] = "Frase secreta",
            [Keys.PassphrasePlaceholder] = "Frase secreta opcional",
            [Keys.PassphraseHelper] = "Déjalo en blanco si usas la cartera estándar.",
            [Keys.PassphraseInfo] = "Las frases secretas distinguen mayúsculas y minúsculas. Un valor incorrecto crea otra cartera oculta.",
            [Keys.ToggleVisibilityLabel] = "Mostrar/ocultar frase",
            [Keys.CancelButtonText] = "Cancelar",
            [Keys.ConfirmButtonText] = "Continuar"
        });
    }
}
