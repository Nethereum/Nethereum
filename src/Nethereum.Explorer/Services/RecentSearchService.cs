namespace Nethereum.Explorer.Services;

public class RecentSearchService
{
    private readonly List<SearchEntry> _searches = new();
    private readonly object _lock = new();
    private const int MaxEntries = 10;

    public IReadOnlyList<SearchEntry> Searches
    {
        get
        {
            lock (_lock)
            {
                return _searches.ToList();
            }
        }
    }

    public void Add(string query, string type, string url)
    {
        lock (_lock)
        {
            _searches.RemoveAll(s => s.Query == query);
            _searches.Insert(0, new SearchEntry { Query = query, Type = type, Url = url });
            if (_searches.Count > MaxEntries)
                _searches.RemoveAt(_searches.Count - 1);
        }
    }

    public class SearchEntry
    {
        public string Query { get; set; } = "";
        public string Type { get; set; } = "";
        public string Url { get; set; } = "";
    }
}
