using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic
{
    public class MnemonicAccountEditorLocalizer : ComponentLocalizerBase<MnemonicAccountCreationViewModel>
    {
        public static class Keys
        {
            public const string DisplayName = "DisplayName";
            public const string Description = "Description";
            public const string AccountNameLabel = "AccountNameLabel";
            public const string AccountNameHelperText = "AccountNameHelperText";
            public const string AccountNamePlaceholder = "AccountNamePlaceholder";
            public const string GenerateTabText = "GenerateTabText";
            public const string ImportTabText = "ImportTabText";
            public const string Generate12WordsText = "Generate12WordsText";
            public const string Generate24WordsText = "Generate24WordsText";
            public const string YourSeedPhraseTitle = "YourSeedPhraseTitle";
            public const string WordCountDisplay = "WordCountDisplay";
            public const string HidePhraseTooltip = "HidePhraseTooltip";
            public const string ShowPhraseTooltip = "ShowPhraseTooltip";
            public const string CopyPhraseTooltip = "CopyPhraseTooltip";
            public const string CopyAllWordsText = "CopyAllWordsText";
            public const string RevealInstructionText = "RevealInstructionText";
            public const string SecurityGuidelinesTitle = "SecurityGuidelinesTitle";
            public const string WriteDownAdvice = "WriteDownAdvice";
            public const string KeepPrivateAdvice = "KeepPrivateAdvice";
            public const string BackupAdvice = "BackupAdvice";
            public const string BackupWarningTitle = "BackupWarningTitle";
            public const string BackupWarningMessage = "BackupWarningMessage";
            public const string BackupConfirmationText = "BackupConfirmationText";
            public const string SeedPhraseLabel = "SeedPhraseLabel";
            public const string SeedPhraseHelperText = "SeedPhraseHelperText";
            public const string PassphraseLabel = "PassphraseLabel";
            public const string PassphraseHelperText = "PassphraseHelperText";
            public const string DerivedAddressLabel = "DerivedAddressLabel";
            public const string CopyAddressTitle = "CopyAddressTitle";
            public const string BackToLoginText = "BackToLoginText";
            public const string BackToAccountSelectionText = "BackToAccountSelectionText";
            public const string AddAccountText = "AddAccountText";
            public const string ValidMnemonicMessage = "ValidMnemonicMessage";
            public const string InvalidMnemonicMessage = "InvalidMnemonicMessage";
            public const string WeakStrengthText = "WeakStrengthText";
            public const string StrongStrengthText = "StrongStrengthText";
            public const string VeryStrongStrengthText = "VeryStrongStrengthText";
            public const string GenerateModeTitle = "GenerateModeTitle";
            public const string GenerateModeDescription = "GenerateModeDescription";
            public const string ImportModeTitle = "ImportModeTitle";
            public const string ImportModeDescription = "ImportModeDescription";
            public const string StepMnemonicGenerate = "StepMnemonicGenerate";
            public const string StepMnemonicImport = "StepMnemonicImport";
            public const string StepSecurityDescription = "StepSecurityDescription";
            public const string WalletNameLabel = "WalletNameLabel";
            public const string WalletNameHelperText = "WalletNameHelperText";
            public const string AccountPreviewTitle = "AccountPreviewTitle";
            public const string StepSetupLabel = "StepSetupLabel";
            public const string StepSeedPhraseLabel = "StepSeedPhraseLabel";
            public const string StepConfirmLabel = "StepConfirmLabel";
            public const string GenerateDescription = "GenerateDescription";
            public const string ImportDescription = "ImportDescription";
            public const string AccountNameDefaultPlaceholder = "AccountNameDefaultPlaceholder";
            public const string AccountNameFinalHelperText = "AccountNameFinalHelperText";
            
            public const string ExitButtonText = "ExitButtonText";
            public const string BackButtonText = "BackButtonText";
            public const string ContinueButtonText = "ContinueButtonText";
            public const string CreateAccountButtonText = "CreateAccountButtonText";
        }
        
        public MnemonicAccountEditorLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Mnemonic Account",
                [Keys.Description] = "Create accounts using a seed phrase (mnemonic) for secure key derivation",
                [Keys.AccountNameLabel] = "Account Name",
                [Keys.AccountNameHelperText] = "Give your account a memorable name (optional)",
                [Keys.AccountNamePlaceholder] = "My Wallet",
                [Keys.GenerateTabText] = "Generate",
                [Keys.ImportTabText] = "Import",
                [Keys.Generate12WordsText] = "Generate 12-Word Phrase",
                [Keys.Generate24WordsText] = "Generate 24-Word Phrase",
                [Keys.YourSeedPhraseTitle] = "Your Seed Phrase",
                [Keys.WordCountDisplay] = "words",
                [Keys.HidePhraseTooltip] = "Hide phrase",
                [Keys.ShowPhraseTooltip] = "Show phrase",
                [Keys.CopyPhraseTooltip] = "Copy entire seed phrase",
                [Keys.CopyAllWordsText] = "Copy All Words",
                [Keys.RevealInstructionText] = "Click the eye icon to reveal your seed phrase",
                [Keys.SecurityGuidelinesTitle] = "Security Guidelines",
                [Keys.WriteDownAdvice] = "Write down your seed phrase on paper and store it safely",
                [Keys.KeepPrivateAdvice] = "Keep it private - never share with anyone",
                [Keys.BackupAdvice] = "Consider multiple secure backup locations",
                [Keys.BackupWarningTitle] = "⚠️ Important: Save Your Seed Phrase",
                [Keys.BackupWarningMessage] = "You must save your seed phrase before continuing. Write it down on paper and store it safely. Without it, you cannot recover your wallet.",
                [Keys.BackupConfirmationText] = "I Have Safely Saved My Seed Phrase",
                [Keys.SeedPhraseLabel] = "Seed Phrase",
                [Keys.SeedPhraseHelperText] = "Enter your 12 or 24-word seed phrase separated by spaces",
                [Keys.PassphraseLabel] = "Passphrase (Optional)",
                [Keys.PassphraseHelperText] = "Optional passphrase for additional security. Note: not all wallets support passphrases",
                [Keys.DerivedAddressLabel] = "Derived Address (Account 0):",
                [Keys.CopyAddressTitle] = "Copy Address",
                [Keys.BackToLoginText] = "Back to Login",
                [Keys.BackToAccountSelectionText] = "Back to Account Selection",
                [Keys.AddAccountText] = "Add Account",
                [Keys.ValidMnemonicMessage] = "Valid mnemonic phrase",
                [Keys.InvalidMnemonicMessage] = "Invalid mnemonic phrase",
                [Keys.WeakStrengthText] = "Weak",
                [Keys.StrongStrengthText] = "Strong",
                [Keys.VeryStrongStrengthText] = "Very Strong",
                [Keys.GenerateModeTitle] = "Generate New Seed Phrase",
                [Keys.GenerateModeDescription] = "We'll create a new seed phrase for you. Make sure to write it down and store it securely.",
                [Keys.ImportModeTitle] = "Import Existing Seed Phrase",
                [Keys.ImportModeDescription] = "Enter your existing 12 or 24 word seed phrase. Never share this with anyone.",
                [Keys.StepMnemonicGenerate] = "Generate your seed phrase",
                [Keys.StepMnemonicImport] = "Enter your seed phrase",
                [Keys.StepSecurityDescription] = "Review and confirm your account details",
                [Keys.WalletNameLabel] = "Wallet Name",
                [Keys.WalletNameHelperText] = "Name for this wallet (contains multiple accounts)",
                [Keys.AccountPreviewTitle] = "Account Preview",
                [Keys.StepSetupLabel] = "Setup",
                [Keys.StepSeedPhraseLabel] = "Seed Phrase",
                [Keys.StepConfirmLabel] = "Confirm",
                [Keys.GenerateDescription] = "Create automatically",
                [Keys.ImportDescription] = "Import existing",
                [Keys.AccountNameDefaultPlaceholder] = "Account 1",
                [Keys.AccountNameFinalHelperText] = "Name for this specific account within your wallet",
                
                [Keys.ExitButtonText] = "Exit",
                [Keys.BackButtonText] = "Back", 
                [Keys.ContinueButtonText] = "Continue",
                [Keys.CreateAccountButtonText] = "Create Account"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Cuenta Mnemotécnica",
                [Keys.Description] = "Crear cuentas usando una frase semilla (mnemotécnica) para derivación segura de claves",
                [Keys.AccountNameLabel] = "Nombre de Cuenta",
                [Keys.AccountNameHelperText] = "Dale un nombre memorable a tu cuenta (opcional)",
                [Keys.AccountNamePlaceholder] = "Mi Cartera",
                [Keys.GenerateTabText] = "Generar",
                [Keys.ImportTabText] = "Importar",
                [Keys.Generate12WordsText] = "Generar Frase de 12 Palabras",
                [Keys.Generate24WordsText] = "Generar Frase de 24 Palabras",
                [Keys.YourSeedPhraseTitle] = "Tu Frase Semilla",
                [Keys.WordCountDisplay] = "palabras",
                [Keys.HidePhraseTooltip] = "Ocultar frase",
                [Keys.ShowPhraseTooltip] = "Mostrar frase",
                [Keys.CopyPhraseTooltip] = "Copiar toda la frase semilla",
                [Keys.CopyAllWordsText] = "Copiar Todas las Palabras",
                [Keys.RevealInstructionText] = "Haz clic en el icono del ojo para revelar tu frase semilla",
                [Keys.SecurityGuidelinesTitle] = "Pautas de Seguridad",
                [Keys.WriteDownAdvice] = "Escribe tu frase semilla en papel y guárdala de forma segura",
                [Keys.KeepPrivateAdvice] = "Mantenla privada - nunca la compartas con nadie",
                [Keys.BackupAdvice] = "Considera múltiples ubicaciones seguras de respaldo",
                [Keys.BackupWarningTitle] = "⚠️ Importante: Guarda Tu Frase Semilla",
                [Keys.BackupWarningMessage] = "Debes guardar tu frase semilla antes de continuar. Escríbela en papel y guárdala de forma segura. Sin ella, no podrás recuperar tu cartera.",
                [Keys.BackupConfirmationText] = "He Guardado Mi Frase Semilla de Forma Segura",
                [Keys.SeedPhraseLabel] = "Frase Semilla",
                [Keys.SeedPhraseHelperText] = "Ingresa tu frase semilla de 12 o 24 palabras separadas por espacios",
                [Keys.PassphraseLabel] = "Frase de Contraseña (Opcional)",
                [Keys.PassphraseHelperText] = "Frase de contraseña opcional para seguridad adicional. Nota: no todas las carteras soportan frases de contraseña",
                [Keys.DerivedAddressLabel] = "Dirección Derivada (Cuenta 0):",
                [Keys.CopyAddressTitle] = "Copiar Dirección",
                [Keys.BackToLoginText] = "Volver al Inicio de Sesión",
                [Keys.BackToAccountSelectionText] = "Volver a Selección de Cuenta",
                [Keys.AddAccountText] = "Añadir Cuenta",
                [Keys.ValidMnemonicMessage] = "Frase mnemotécnica válida",
                [Keys.InvalidMnemonicMessage] = "Frase mnemotécnica inválida",
                [Keys.WeakStrengthText] = "Débil",
                [Keys.StrongStrengthText] = "Fuerte",
                [Keys.VeryStrongStrengthText] = "Muy Fuerte",
                [Keys.GenerateModeTitle] = "Generar Nueva Frase Semilla",
                [Keys.GenerateModeDescription] = "Crearemos una nueva frase semilla para ti. Asegúrate de escribirla y guardarla de forma segura.",
                [Keys.ImportModeTitle] = "Importar Frase Semilla Existente",
                [Keys.ImportModeDescription] = "Ingresa tu frase semilla existente de 12 o 24 palabras. Nunca la compartas con nadie.",
                [Keys.StepMnemonicGenerate] = "Genera tu frase semilla",
                [Keys.StepMnemonicImport] = "Ingresa tu frase semilla",
                [Keys.StepSecurityDescription] = "Revisa y confirma los detalles de tu cuenta",
                [Keys.WalletNameLabel] = "Nombre de Cartera",
                [Keys.WalletNameHelperText] = "Nombre para esta cartera (contiene múltiples cuentas)",
                [Keys.AccountPreviewTitle] = "Vista Previa de Cuenta",
                [Keys.StepSetupLabel] = "Configuración",
                [Keys.StepSeedPhraseLabel] = "Frase Semilla",
                [Keys.StepConfirmLabel] = "Confirmar",
                [Keys.GenerateDescription] = "Crear automáticamente",
                [Keys.ImportDescription] = "Importar existente",
                [Keys.AccountNameDefaultPlaceholder] = "Cuenta 1",
                [Keys.AccountNameFinalHelperText] = "Nombre para esta cuenta específica dentro de tu cartera",
                
                [Keys.ExitButtonText] = "Salir",
                [Keys.BackButtonText] = "Atrás",
                [Keys.ContinueButtonText] = "Continuar", 
                [Keys.CreateAccountButtonText] = "Crear Cuenta"
            });
        }
    }
}