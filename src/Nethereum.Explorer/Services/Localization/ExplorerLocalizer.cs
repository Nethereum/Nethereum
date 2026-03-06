namespace Nethereum.Explorer.Services.Localization;

public class ExplorerLocalizer
{
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new();
    private string _currentLanguage = "en";

    public string this[string key] => GetString(key);

    public string GetString(string key)
    {
        if (_translations.TryGetValue(_currentLanguage, out var lang) && lang.TryGetValue(key, out var val))
            return val;
        if (_translations.TryGetValue("en", out var en) && en.TryGetValue(key, out var fallback))
            return fallback;
        return key;
    }

    public string GetString(string key, params object[] args)
    {
        var template = GetString(key);
        return string.Format(template, args);
    }

    public string CurrentLanguage => _currentLanguage;

    public event Action? LanguageChanged;

    public void SetLanguage(string lang)
    {
        _currentLanguage = lang;
        LanguageChanged?.Invoke();
    }

    public ExplorerLocalizer()
    {
        Register("en", ExplorerStrings_en.All);
        Register("es", ExplorerStrings_es.All);
    }

    private void Register(string lang, Dictionary<string, string> strings)
        => _translations[lang] = strings;
}
