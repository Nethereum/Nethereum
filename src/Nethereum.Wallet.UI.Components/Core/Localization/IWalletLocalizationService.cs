using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Core.Localization
{
    public interface IWalletLocalizationService
    {
        string CurrentLanguage { get; }
        CultureInfo CurrentCulture { get; }
        IReadOnlyList<LanguageInfo> AvailableLanguages { get; }
        event Action<string> LanguageChanged;
        Task SetLanguageAsync(string languageCode);
        Task<string> DetectAndSetLanguageAsync();
        void RegisterTranslations(string componentName, string language, Dictionary<string, string> translations);
        void OverrideTranslation(string componentName, string language, string key, string value);
        string GetTranslation(string componentName, string language, string key);
        string GetTranslation(string componentName, string language, string key, params object[] args);
        IComponentLocalizer<T> GetLocalizer<T>();
        void RegisterLocalizer<T>(IComponentLocalizer<T> localizer);
        Task LoadTranslationsAsync(string componentName, string language, string json);
        bool IsLanguageSupported(string languageCode);
        void AddLanguageSupport(string languageCode, string defaultCulture);
        void SetDefaultLanguage(string defaultCulture);
    }
    public class LanguageInfo
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string NativeName { get; set; }
        public bool IsRTL { get; set; }
        public string Culture { get; set; }
    }
}