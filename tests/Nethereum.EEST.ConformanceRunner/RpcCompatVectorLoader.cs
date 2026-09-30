using System.Collections.Generic;
using System.IO;

namespace Nethereum.EEST.ConformanceRunner
{
    public static class RpcCompatVectorLoader
    {
        public sealed class Exchange
        {
            public string RequestJson { get; init; } = "";
            public string ExpectedResponseJson { get; init; } = "";

            public bool SchemaOnly { get; init; }
        }

        public static List<Exchange> Load(string file)
        {
            var lines = File.ReadAllLines(file);

            var schemaOnly = false;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("//") && line.Contains("speconly"))
                {
                    schemaOnly = true;
                    break;
                }
            }

            var exchanges = new List<Exchange>();
            string pendingRequest = null;

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//")) continue;

                if (line.StartsWith(">>"))
                {
                    pendingRequest = line.Substring(2).Trim();
                }
                else if (line.StartsWith("<<") && pendingRequest != null)
                {
                    exchanges.Add(new Exchange
                    {
                        RequestJson = pendingRequest,
                        ExpectedResponseJson = line.Substring(2).Trim(),
                        SchemaOnly = schemaOnly
                    });
                    pendingRequest = null;
                }
            }

            return exchanges;
        }

        public static string MethodOf(string file)
        {
            var name = Path.GetFileName(Path.GetDirectoryName(file));
            return string.IsNullOrEmpty(name) ? Path.GetFileNameWithoutExtension(file) : name;
        }
    }
}
