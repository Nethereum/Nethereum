namespace Nethereum.EEST.Conformance
{
    public abstract class CorpusSource : ICorpusSource
    {
        private readonly Lazy<IReadOnlyList<string>> _files;
        private readonly Lazy<IReadOnlyList<ConformanceCase>> _cases;
        private readonly Lazy<Dictionary<string, ConformanceCase>> _byId;

        private readonly string _idPrefix;

        protected CorpusSource(CaseKind kind, string root, string filePattern, string idPrefix = null)
        {
            Kind = kind;
            Root = root;
            _idPrefix = idPrefix ?? Path.GetFileName(Path.TrimEndingDirectorySeparator(root));
            _files = new(() => IsPresent
                ? Directory.GetFiles(Root, filePattern, SearchOption.AllDirectories)
                    .Where(f => !IsInHiddenDirectory(f))
                    .OrderBy(f => f, StringComparer.Ordinal).ToList()
                : new List<string>());
            _cases = new(() => Files.SelectMany(ReadCases).ToList());
            _byId = new(() =>
            {
                var map = new Dictionary<string, ConformanceCase>(StringComparer.Ordinal);
                foreach (var c in Cases) map.TryAdd(c.Id, c);
                return map;
            });
        }

        public CaseKind Kind { get; }
        public string Root { get; }
        public virtual bool IsPresent => Directory.Exists(Root);
        public IReadOnlyList<string> Files => _files.Value;
        public IReadOnlyList<ConformanceCase> Cases => _cases.Value;

        public ConformanceCase Find(string id) => _byId.Value.TryGetValue(id, out var c) ? c : null;

        private bool IsInHiddenDirectory(string file) =>
            Path.GetRelativePath(Root, file)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .SkipLast(1)
                .Any(segment => segment.StartsWith('.'));

        protected abstract IEnumerable<ConformanceCase> ReadCases(string file);

        protected string RelativeId(string file) =>
            $"{_idPrefix}/{Path.GetRelativePath(Root, file).Replace(Path.DirectorySeparatorChar, '/')}";
    }
}
