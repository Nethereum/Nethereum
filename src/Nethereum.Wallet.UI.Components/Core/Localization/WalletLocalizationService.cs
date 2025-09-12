using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Core.Localization
{
    public class WalletLocalizationService : IWalletLocalizationService
    {
        private readonly ILocalizationStorageProvider _storageProvider;
        private readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> _translations;
        private readonly Dictionary<Type, object> _localizers;
        private readonly Dictionary<string, string> _languageDefaults;
        private CultureInfo _currentCulture = new CultureInfo("en-US");
        private string _defaultLanguage = "en-US";
        
        public string CurrentLanguage => _currentCulture.TwoLetterISOLanguageName;
        public CultureInfo CurrentCulture => _currentCulture;
        public IReadOnlyList<LanguageInfo> AvailableLanguages { get; } = new List<LanguageInfo>
        {
            new LanguageInfo { Code = "en", Name = "English", NativeName = "English", Culture = "en-US" },
            new LanguageInfo { Code = "es", Name = "Spanish", NativeName = "Español", Culture = "es-ES" }
        };
        
        public event Action<string> LanguageChanged;
        
        public WalletLocalizationService(ILocalizationStorageProvider storageProvider = null)
        {
            _storageProvider = storageProvider;
            _translations = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
            _localizers = new Dictionary<Type, object>();
            _languageDefaults = new Dictionary<string, string>
            {
                ["en"] = "en-US",
                ["es"] = "es-ES"
            };
        }
        
        public async Task SetLanguageAsync(string languageCode)
        {
            try
            {
                _currentCulture = new CultureInfo(languageCode);
                
                if (_storageProvider != null)
                {
                    await _storageProvider.SetStoredLanguageAsync(languageCode);
                }
                
                LanguageChanged?.Invoke(CurrentLanguage);
            }
            catch
            {
            }
        }
        
        public async Task<string> DetectAndSetLanguageAsync()
        {
            if (_storageProvider != null)
            {
                var storedLang = await _storageProvider.GetStoredLanguageAsync();
                if (!string.IsNullOrEmpty(storedLang))
                {
                    await SetLanguageAsync(storedLang);
                    return CurrentLanguage;
                }
                
                var systemLang = await _storageProvider.GetSystemLanguageAsync();
                if (!string.IsNullOrEmpty(systemLang))
                {
                    await SetLanguageAsync(systemLang);
                }
            }
            
            return CurrentLanguage;
        }
        
        public void RegisterTranslations(string componentName, string language, Dictionary<string, string> translations)
        {
            if (!_translations.ContainsKey(componentName))
                _translations[componentName] = new Dictionary<string, Dictionary<string, string>>();
                
            _translations[componentName][language] = translations;
        }
        
        public void OverrideTranslation(string componentName, string language, string key, string value)
        {
            if (!_translations.ContainsKey(componentName))
                _translations[componentName] = new Dictionary<string, Dictionary<string, string>>();
                
            if (!_translations[componentName].ContainsKey(language))
                _translations[componentName][language] = new Dictionary<string, string>();
                
            _translations[componentName][language][key] = value;
        }
        
        public string GetTranslation(string componentName, string language, string key)
        {
            if (_translations.TryGetValue(componentName, out var componentTranslations))
            {
                // 1. Try exact culture match (e.g., "es-ES")
                var currentCultureName = _currentCulture.Name;
                if (componentTranslations.TryGetValue(currentCultureName, out var exactTranslations))
                {
                    if (exactTranslations.TryGetValue(key, out var translation))
                        return translation;
                }
                
                // 2. Try language default mapping (e.g., "es" → "es-ES")
                var languageCode = _currentCulture.TwoLetterISOLanguageName;
                if (_languageDefaults.TryGetValue(languageCode, out var defaultCulture))
                {
                    if (componentTranslations.TryGetValue(defaultCulture, out var defaultTranslations))
                    {
                        if (defaultTranslations.TryGetValue(key, out var translation))
                            return translation;
                    }
                }
                
                if (currentCultureName != _defaultLanguage && componentTranslations.TryGetValue(_defaultLanguage, out var fallbackTranslations))
                {
                    if (fallbackTranslations.TryGetValue(key, out var translation))
                        return translation;
                }
            }
            
            return key;
        }
        
        public string GetTranslation(string componentName, string language, string key, params object[] args)
        {
            var format = GetTranslation(componentName, language, key);
            return string.Format(_currentCulture, format, args);
        }
        
        public IComponentLocalizer<T> GetLocalizer<T>()
        {
            var type = typeof(T);
            if (!_localizers.ContainsKey(type))
            {
                _localizers[type] = new ComponentLocalizer<T>(this);
            }
            return (IComponentLocalizer<T>)_localizers[type];
        }
        
        public void RegisterLocalizer<T>(IComponentLocalizer<T> localizer)
        {
            _localizers[typeof(T)] = localizer;
        }
        
        public async Task LoadTranslationsAsync(string componentName, string language, string json)
        {
            var translations = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (translations != null)
            {
                RegisterTranslations(componentName, language, translations);
            }
        }
        
        public bool IsLanguageSupported(string languageCode)
        {
            try
            {
                var culture = new CultureInfo(languageCode);
                var language = culture.TwoLetterISOLanguageName;
                return _languageDefaults.ContainsKey(language);
            }
            catch
            {
                return false;
            }
        }
        
        public void AddLanguageSupport(string languageCode, string defaultCulture)
        {
            _languageDefaults[languageCode] = defaultCulture;
        }
        
        public void SetDefaultLanguage(string defaultCulture)
        {
            _defaultLanguage = defaultCulture;
        }
    }
    
    internal class ComponentLocalizer<T> : IComponentLocalizer<T>
    {
        private readonly IWalletLocalizationService _localizationService;
        private readonly string _componentName;
        
        public ComponentLocalizer(IWalletLocalizationService localizationService)
        {
            _localizationService = localizationService;
            _componentName = typeof(T).FullName ?? typeof(T).Name;
        }
        
        public string GetString(string key)
        {
            return _localizationService.GetTranslation(_componentName, _localizationService.CurrentLanguage, key);
        }
        
        public string GetString(string key, params object[] args)
        {
            return _localizationService.GetTranslation(_componentName, _localizationService.CurrentLanguage, key, args);
        }
    }
}