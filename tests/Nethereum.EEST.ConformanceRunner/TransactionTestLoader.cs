using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Nethereum.EEST.ConformanceRunner
{
    public static class TransactionTestLoader
    {
        public sealed class TransactionTest
        {
            public string Name { get; init; } = "";
            public string TxBytes { get; init; } = "";
            public List<ForkResult> Results { get; init; } = new();
        }

        public sealed class ForkResult
        {
            public string Fork { get; init; } = "";
            public string IntrinsicGas { get; init; }
            public string Exception { get; init; }
            public string Sender { get; init; }
            public string Hash { get; init; }

            public bool IsRejection => !string.IsNullOrEmpty(Exception);
        }

        public static List<TransactionTest> LoadFromFile(string file)
        {
            using var stream = File.OpenRead(file);
            using var doc = JsonDocument.Parse(stream);

            var tests = new List<TransactionTest>();
            foreach (var testProp in doc.RootElement.EnumerateObject())
            {
                var results = new List<ForkResult>();
                if (testProp.Value.TryGetProperty("result", out var result))
                {
                    foreach (var forkProp in result.EnumerateObject())
                    {
                        var r = forkProp.Value;
                        results.Add(new ForkResult
                        {
                            Fork = forkProp.Name,
                            IntrinsicGas = GetStringOrNull(r, "intrinsicGas"),
                            Exception = GetStringOrNull(r, "exception"),
                            Sender = GetStringOrNull(r, "sender"),
                            Hash = GetStringOrNull(r, "hash")
                        });
                    }
                }

                tests.Add(new TransactionTest
                {
                    Name = testProp.Name,
                    TxBytes = GetStringOrNull(testProp.Value, "txbytes") ?? "",
                    Results = results
                });
            }

            return tests;
        }

        private static string GetStringOrNull(JsonElement element, string property) =>
            element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString()
                : null;
    }
}
