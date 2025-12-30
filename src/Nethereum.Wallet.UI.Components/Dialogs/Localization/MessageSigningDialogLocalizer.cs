using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Dialogs.Localization
{
    public class MessageSigningDialogLocalizer : ComponentLocalizerBase<MessageSigningDialogLocalizer>
    {
        public static class Keys
        {
            public const string HighRiskSigningRequest = "HighRiskSigningRequest";
            public const string RequestFrom = "RequestFrom";
            public const string DomainInformation = "DomainInformation";
            public const string Name = "Name";
            public const string Version = "Version";
            public const string Contract = "Contract";
            public const string StructuredData = "StructuredData";
            public const string Message = "Message";
            public const string ShowStructuredView = "ShowStructuredView";
            public const string ShowRawJson = "ShowRawJson";
            public const string Chain = "Chain";
            public const string SecurityNotice = "SecurityNotice";
            public const string TypedDataSecurityMessage = "TypedDataSecurityMessage";
            public const string PersonalSignSecurityMessage = "PersonalSignSecurityMessage";
            public const string Cancel = "Cancel";
            public const string Signing = "Signing";
            public const string SignMessage = "SignMessage";
            public const string UnableToDisplayData = "UnableToDisplayData";
        }

        public MessageSigningDialogLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.HighRiskSigningRequest] = "High Risk Signing Request",
                [Keys.RequestFrom] = "Request from:",
                [Keys.DomainInformation] = "Domain Information",
                [Keys.Name] = "Name:",
                [Keys.Version] = "Version:",
                [Keys.Contract] = "Contract:",
                [Keys.StructuredData] = "Structured Data",
                [Keys.Message] = "Message",
                [Keys.ShowStructuredView] = "Show Structured View",
                [Keys.ShowRawJson] = "Show Raw JSON",
                [Keys.Chain] = "Chain",
                [Keys.SecurityNotice] = "Security Notice",
                [Keys.TypedDataSecurityMessage] = "Signing this structured data will authorize the action described above. Only sign if you trust the requesting application.",
                [Keys.PersonalSignSecurityMessage] = "Signing this message proves you own this account. Only sign if you trust the requesting application.",
                [Keys.Cancel] = "Cancel",
                [Keys.Signing] = "Signing...",
                [Keys.SignMessage] = "Sign Message",
                [Keys.UnableToDisplayData] = "Unable to display structured data"
            });

            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.HighRiskSigningRequest] = "Solicitud de Firma de Alto Riesgo",
                [Keys.RequestFrom] = "Solicitud de:",
                [Keys.DomainInformation] = "Información del Dominio",
                [Keys.Name] = "Nombre:",
                [Keys.Version] = "Versión:",
                [Keys.Contract] = "Contrato:",
                [Keys.StructuredData] = "Datos Estructurados",
                [Keys.Message] = "Mensaje",
                [Keys.ShowStructuredView] = "Mostrar Vista Estructurada",
                [Keys.ShowRawJson] = "Mostrar JSON Sin Procesar",
                [Keys.Chain] = "Cadena",
                [Keys.SecurityNotice] = "Aviso de Seguridad",
                [Keys.TypedDataSecurityMessage] = "Firmar estos datos estructurados autorizará la acción descrita arriba. Solo firma si confías en la aplicación solicitante.",
                [Keys.PersonalSignSecurityMessage] = "Firmar este mensaje demuestra que eres dueño de esta cuenta. Solo firma si confías en la aplicación solicitante.",
                [Keys.Cancel] = "Cancelar",
                [Keys.Signing] = "Firmando...",
                [Keys.SignMessage] = "Firmar Mensaje",
                [Keys.UnableToDisplayData] = "No se pueden mostrar los datos estructurados"
            });
        }
    }
}
