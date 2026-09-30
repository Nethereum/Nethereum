using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class HiveResultsSink : IDisposable
    {
        private readonly ConcurrentDictionary<string, HiveTestCase> _cases = new();
        private readonly DateTimeOffset _suiteStart = DateTimeOffset.UtcNow;
        private int _passed;
        private int _failed;
        private string _suiteId = "unconfigured-suite";
        private string _suiteName = "Unconfigured conformance suite";

        public static readonly string ResultsDir = Path.Combine(
            FixtureCatalog.RepoRoot ?? Directory.GetCurrentDirectory(),
            ".testresults", "hive", "workspace", "logs");

        public void EnsureSuite(IConformanceDriver driver)
        {
            if (_suiteId == driver.SuiteId) return;
            if (_suiteId != "unconfigured-suite")
                throw new InvalidOperationException(
                    $"HiveResultsSink already configured for suite '{_suiteId}'; refusing to also report '{driver.SuiteId}' " +
                    "into the same file - give each driver its own collection/sink.");

            _suiteId = driver.SuiteId;
            _suiteName = driver.SuiteName;
        }

        public void Record(string caseId, string name, bool pass, string details, DateTimeOffset start, DateTimeOffset end)
        {
            _cases[caseId] = new HiveTestCase
            {
                Name = name,
                SummaryResult = new HiveSummaryResult { Pass = pass, Details = details },
                Start = start,
                End = end
            };

            if (pass) Interlocked.Increment(ref _passed);
            else Interlocked.Increment(ref _failed);
        }

        public (int Passed, int Failed, int Total) Counts => (_passed, _failed, _cases.Count);

        public void Dispose()
        {
            if (_cases.IsEmpty) return;

            try
            {
                var suiteDir = Path.Combine(ResultsDir, _suiteId);
                Directory.CreateDirectory(suiteDir);

                var suite = new HiveSuite
                {
                    Name = _suiteName,
                    Description = "In-process conformance run via Nethereum.EEST.ConformanceRunner - no Docker, no hive orchestrator.",
                    ClientVersions = new Dictionary<string, string>
                    {
                        ["nethereum"] = typeof(HiveResultsSink).Assembly.GetName().Version?.ToString() ?? "dev"
                    },
                    SimLog = "",
                    TestCases = _cases,
                    Start = _suiteStart,
                    End = DateTimeOffset.UtcNow,
                };

                var fileName = $"{_suiteId}-{_suiteStart:yyyyMMdd-HHmmss}.json";
                var path = Path.Combine(suiteDir, fileName);
                var json = JsonSerializer.Serialize(suite, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
                File.WriteAllText(path, json);

                Console.WriteLine($"[hive-json] wrote {_cases.Count} test cases ({_passed} pass / {_failed} fail) to {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[hive-json] FAILED to write results: {ex}");
            }
        }
    }

    public sealed class HiveSuite
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("clientVersions")] public Dictionary<string, string> ClientVersions { get; set; } = new();
        [JsonPropertyName("simLog")] public string SimLog { get; set; } = "";
        [JsonPropertyName("testCases")] public ConcurrentDictionary<string, HiveTestCase> TestCases { get; set; } = new();
        [JsonPropertyName("start")] public DateTimeOffset Start { get; set; }
        [JsonPropertyName("end")] public DateTimeOffset End { get; set; }
    }

    public sealed class HiveTestCase
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("summaryResult")] public HiveSummaryResult SummaryResult { get; set; } = new();
        [JsonPropertyName("start")] public DateTimeOffset Start { get; set; }
        [JsonPropertyName("end")] public DateTimeOffset End { get; set; }
    }

    public sealed class HiveSummaryResult
    {
        [JsonPropertyName("pass")] public bool Pass { get; set; }
        [JsonPropertyName("details")] public string Details { get; set; } = "";
    }
}
