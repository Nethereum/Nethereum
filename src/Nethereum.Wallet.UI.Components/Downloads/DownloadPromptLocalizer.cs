using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Downloads
{
    public class DownloadPromptLocalizer : ComponentLocalizerBase<object>
    {
        public static class Keys
        {
            public const string DownloadTitle = "DownloadTitle";
            public const string DownloadConfirmMessage = "DownloadConfirmMessage";
            public const string SaveButton = "SaveButton";
            public const string CancelButton = "CancelButton";
            public const string DownloadSuccess = "DownloadSuccess";
            public const string DownloadFailed = "DownloadFailed";
            public const string DownloadStarted = "DownloadStarted";
            public const string FileSize = "FileSize";
            public const string FileType = "FileType";
        }

        public DownloadPromptLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.DownloadTitle] = "Download File",
                [Keys.DownloadConfirmMessage] = "Do you want to download \"{0}\"?",
                [Keys.SaveButton] = "Save",
                [Keys.CancelButton] = "Cancel",
                [Keys.DownloadSuccess] = "Downloaded: {0}",
                [Keys.DownloadFailed] = "Download failed: {0}",
                [Keys.DownloadStarted] = "Downloading: {0}",
                [Keys.FileSize] = "Size: {0}",
                [Keys.FileType] = "Type: {0}"
            });

            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.DownloadTitle] = "Descargar Archivo",
                [Keys.DownloadConfirmMessage] = "\u00bfDesea descargar \"{0}\"?",
                [Keys.SaveButton] = "Guardar",
                [Keys.CancelButton] = "Cancelar",
                [Keys.DownloadSuccess] = "Descargado: {0}",
                [Keys.DownloadFailed] = "Error en la descarga: {0}",
                [Keys.DownloadStarted] = "Descargando: {0}",
                [Keys.FileSize] = "Tama\u00f1o: {0}",
                [Keys.FileType] = "Tipo: {0}"
            });
        }
    }
}
