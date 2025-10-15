using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Prompts
{
    public class DAppPermissionPromptLocalizer : ComponentLocalizerBase<DAppPermissionPromptViewModel>
    {
        public DAppPermissionPromptLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        public static class Keys
        {
            public const string Title = "Title";
            public const string Subtitle = "Subtitle";
            public const string OriginLabel = "OriginLabel";
            public const string AccountLabel = "AccountLabel";
            public const string Approve = "Approve";
            public const string Reject = "Reject";
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.Title] = "Connect Request",
                [Keys.Subtitle] = "This dapp is requesting access to your account",
                [Keys.OriginLabel] = "Origin",
                [Keys.AccountLabel] = "Account",
                [Keys.Approve] = "Approve",
                [Keys.Reject] = "Reject"
            });

            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.Title] = "Solicitud de conexión",
                [Keys.Subtitle] = "Esta dapp solicita acceso a tu cuenta",
                [Keys.OriginLabel] = "Origen",
                [Keys.AccountLabel] = "Cuenta",
                [Keys.Approve] = "Aprobar",
                [Keys.Reject] = "Rechazar"
            });
        }
    }
}
