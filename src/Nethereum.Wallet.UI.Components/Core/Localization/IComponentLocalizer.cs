namespace Nethereum.Wallet.UI.Components.Core.Localization
{
    public interface IComponentLocalizer
    {
        string GetString(string key);
        string GetString(string key, params object[] args);
    }
    public interface IComponentLocalizer<TComponent> : IComponentLocalizer
    {
    }
    public class DefaultComponentLocalizer<TComponent> : IComponentLocalizer<TComponent>
    {
        private readonly Dictionary<string, string> _translations;

        public DefaultComponentLocalizer(Dictionary<string, string> translations)
        {
            _translations = translations ?? new Dictionary<string, string>();
        }

        public virtual string GetString(string key)
        {
            return _translations.TryGetValue(key, out var value) ? value : key;
        }

        public virtual string GetString(string key, params object[] args)
        {
            var format = GetString(key);
            return args.Length > 0 ? string.Format(format, args) : format;
        }
    }
}