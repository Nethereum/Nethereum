using System.Collections.Generic;

namespace Nethereum.Wallet.UI.Components.Core.Localization
{
    public abstract class ComponentLocalizerBase<T> : IComponentLocalizer<T>
    {
        protected readonly IWalletLocalizationService _globalService;
        protected readonly string _componentName;
        
        protected ComponentLocalizerBase(IWalletLocalizationService globalService)
        {
            _globalService = globalService;
            _componentName = typeof(T).FullName ?? typeof(T).Name;
            RegisterTranslations();
        }
        
        public string GetString(string key)
        {
            return _globalService.GetTranslation(_componentName, _globalService.CurrentLanguage, key);
        }
        
        public string GetString(string key, params object[] args)
        {
            return _globalService.GetTranslation(_componentName, _globalService.CurrentLanguage, key, args);
        }
        protected abstract void RegisterTranslations();
    }
}