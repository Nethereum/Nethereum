using Nethereum.Wallet.UI.Components.Core.Localization;
using System.Collections.Generic;

namespace Nethereum.Wallet.UI.Components.Blazor.Prompts
{
    public sealed class DAppSignaturePromptLocalizer : ComponentLocalizerBase<DAppSignaturePromptView>
    {
        public DAppSignaturePromptLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        public static class Keys
        {
            public const string Title = "Title";
            public const string SubtitleFrom = "SubtitleFrom";
            public const string SubtitleGeneric = "SubtitleGeneric";
            public const string RequestDetails = "RequestDetails";
            public const string AccountLabel = "AccountLabel";
            public const string MessageLabel = "MessageLabel";
            public const string HexChip = "HexChip";
            public const string TooltipViewRaw = "TooltipViewRaw";
            public const string TooltipViewDecoded = "TooltipViewDecoded";
            public const string Warning = "Warning";
            public const string Reject = "Reject";
            public const string Sign = "Sign";
            public const string AccountDisplay = "AccountDisplay";
            public const string DomainSectionTitle = "DomainSectionTitle";
            public const string DomainName = "DomainName";
            public const string DomainVersion = "DomainVersion";
            public const string VerifyingContract = "VerifyingContract";
            public const string PrimaryType = "PrimaryType";
            public const string ChainId = "ChainId";
            public const string HashSectionTitle = "HashSectionTitle";
            public const string DomainHashLabel = "DomainHashLabel";
            public const string MessageHashLabel = "MessageHashLabel";
            public const string TypedDataHashLabel = "TypedDataHashLabel";
            public const string SignaturePreviewTitle = "SignaturePreviewTitle";
            public const string CopySignature = "CopySignature";
            public const string SignatureReviewInfo = "SignatureReviewInfo";
            public const string ConfirmSignature = "ConfirmSignature";
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.Title] = "Signature Request",
                [Keys.SubtitleFrom] = "Request from {0}",
                [Keys.SubtitleGeneric] = "Signature request from application",
                [Keys.RequestDetails] = "Request Details",
                [Keys.AccountLabel] = "Account",
                [Keys.MessageLabel] = "Message",
                [Keys.HexChip] = "Hex",
                [Keys.TooltipViewRaw] = "View raw",
                [Keys.TooltipViewDecoded] = "View decoded",
                [Keys.Warning] = "Only sign messages from applications you trust. Signing malicious messages can result in loss of funds.",
                [Keys.Reject] = "Reject",
                [Keys.Sign] = "Sign",
                [Keys.AccountDisplay] = "Account: {0}",
                [Keys.DomainSectionTitle] = "Typed Data Domain",
                [Keys.DomainName] = "Domain",
                [Keys.DomainVersion] = "Version",
                [Keys.VerifyingContract] = "Verifying Contract",
                [Keys.PrimaryType] = "Primary Type",
                [Keys.ChainId] = "Chain ID",
                [Keys.HashSectionTitle] = "Typed Data Hashes",
                [Keys.DomainHashLabel] = "Domain Hash",
                [Keys.MessageHashLabel] = "Message Hash",
                [Keys.TypedDataHashLabel] = "Typed Data Hash",
                [Keys.SignaturePreviewTitle] = "Signature Result",
                [Keys.CopySignature] = "Copy Signature",
                [Keys.SignatureReviewInfo] = "Review the signature and confirm to send it back to the application.",
                [Keys.ConfirmSignature] = "Submit"
            });

            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.Title] = "Solicitud de Firma",
                [Keys.SubtitleFrom] = "Solicitud de {0}",
                [Keys.SubtitleGeneric] = "Solicitud de firma de la aplicación",
                [Keys.RequestDetails] = "Detalles de la Solicitud",
                [Keys.AccountLabel] = "Cuenta",
                [Keys.MessageLabel] = "Mensaje",
                [Keys.HexChip] = "Hex",
                [Keys.TooltipViewRaw] = "Ver original",
                [Keys.TooltipViewDecoded] = "Ver decodificado",
                [Keys.Warning] = "Solo firme mensajes de dapps en las que confíe. Firmar mensajes maliciosos puede resultar en la pérdida de fondos.",
                [Keys.Reject] = "Rechazar",
                [Keys.Sign] = "Firmar",
                [Keys.AccountDisplay] = "Cuenta: {0}",
                [Keys.DomainSectionTitle] = "Dominio de Datos Tipados",
                [Keys.DomainName] = "Dominio",
                [Keys.DomainVersion] = "Versión",
                [Keys.VerifyingContract] = "Contrato Verificador",
                [Keys.PrimaryType] = "Tipo Principal",
                [Keys.ChainId] = "ID de Cadena",
                [Keys.HashSectionTitle] = "Hashes de Datos Tipados",
                [Keys.DomainHashLabel] = "Hash del Dominio",
                [Keys.MessageHashLabel] = "Hash del Mensaje",
                [Keys.TypedDataHashLabel] = "Hash Final",
                [Keys.SignaturePreviewTitle] = "Resultado de la Firma",
                [Keys.CopySignature] = "Copiar Firma",
                [Keys.SignatureReviewInfo] = "Revise la firma y confirme para devolverla a la aplicación.",
                [Keys.ConfirmSignature] = "Enviar"
            });
        }
    }
}
