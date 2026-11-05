using System.Text.Json;

namespace Nethereum.EEST.Conformance
{
    public sealed class EestFixtureSource : CorpusSource
    {
        public EestFixtureSource(CaseKind kind, string root, string idPrefix = null) : base(kind, root, "*.json", idPrefix)
        {
        }

        protected override IEnumerable<ConformanceCase> ReadCases(string file) =>
            Kind == CaseKind.State ? ReadStateCases(file) : ReadNamedCases(file);

        private IEnumerable<ConformanceCase> ReadNamedCases(string file)
        {
            var prefix = RelativeId(file);
            var bytes = System.IO.File.ReadAllBytes(file);
            var reader = new Utf8JsonReader(bytes, isFinalBlock: true, state: default);
            var cases = new List<ConformanceCase>();

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                return cases;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName) continue;

                var name = reader.GetString() ?? "";
                cases.Add(new ConformanceCase(Kind, $"{prefix}::{name}", file, name));
                reader.Skip();
            }

            return cases;
        }

        private IEnumerable<ConformanceCase> ReadStateCases(string file)
        {
            var prefix = RelativeId(file);
            var cases = new List<ConformanceCase>();

            using var stream = System.IO.File.OpenRead(file);
            using var doc = JsonDocument.Parse(stream);

            foreach (var testProp in doc.RootElement.EnumerateObject())
            {
                if (!testProp.Value.TryGetProperty("post", out var post)) continue;

                foreach (var forkProp in post.EnumerateObject())
                {
                    foreach (var entry in forkProp.Value.EnumerateArray())
                    {
                        int d = 0, g = 0, v = 0;
                        if (entry.TryGetProperty("indexes", out var idx))
                        {
                            if (idx.TryGetProperty("data", out var dv) && dv.ValueKind == JsonValueKind.Number) d = dv.GetInt32();
                            if (idx.TryGetProperty("gas", out var gv) && gv.ValueKind == JsonValueKind.Number) g = gv.GetInt32();
                            if (idx.TryGetProperty("value", out var vv) && vv.ValueKind == JsonValueKind.Number) v = vv.GetInt32();
                        }

                        cases.Add(new ConformanceCase(Kind, $"{prefix}::{ConformanceCase.DisplayNameOf(testProp.Name, forkProp.Name, d, g, v)}", file, testProp.Name, forkProp.Name, d, g, v));
                    }
                }
            }

            return cases;
        }
    }
}
