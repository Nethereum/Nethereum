using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.EEST.Conformance.Diagnosis;
using Nethereum.Testing;

namespace Nethereum.DevP2P.IntegrationTests.GethReference
{
    public sealed class GethAnswer
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public string Canonical { get; set; }
        public string Hash { get; set; }
        public JsonElement Extra { get; set; }

        public bool HasExtra => Extra.ValueKind == JsonValueKind.Object;

        public bool ItemsOk
        {
            get
            {
                if (!HasExtra || !Extra.TryGetProperty("items_ok", out var items)) return true;
                return items.ValueKind != JsonValueKind.False;
            }
        }

        public string PolicyRefusal =>
            HasExtra && Extra.TryGetProperty("policy_refuse", out var reason) ? reason.GetString() : null;

        public long? Number(string name) =>
            HasExtra && Extra.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : (long?)null;

        public ulong? Unsigned(string name) =>
            HasExtra && Extra.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out var number) ? number : (ulong?)null;

        public string Text(string name) =>
            HasExtra && Extra.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        public string[] Strings(string name)
        {
            if (!HasExtra || !Extra.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) return null;
            var list = new List<string>();
            foreach (var item in value.EnumerateArray()) list.Add(item.GetString());
            return list.ToArray();
        }
    }

    public sealed class GethReferenceProcess : IDisposable
    {
        public const string ToolKey = "GethReferenceDecoder";

        private const long MemoryCapBytes = 3L * 1024 * 1024 * 1024;

        private readonly Process _process;
        private readonly StringBuilder _stderr = new StringBuilder();
        private readonly CancellationTokenSource _lifetime;

        public GethReferenceProcess(TimeSpan lifetime)
        {
            var path = TestEnvironment.Tool(ToolKey);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Tools:{ToolKey} (the geth v1.17.4 reference decoder) was not found at '{path}'. " +
                    $"Run `dotnet run ci/fetch-externals.cs geth-tools` (needs Go), or set Tools:{ToolKey} in {TestEnvironment.LocalSettingsRelativePath}.");

            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = new UTF8Encoding(false)
            };
            startInfo.Environment["GOMEMLIMIT"] = "2GiB";
            _process = new Process { StartInfo = startInfo };
            _process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (_stderr) _stderr.AppendLine(e.Data); };
            _process.Start();
            ChildProcessReaper.Register(_process);
            _process.BeginErrorReadLine();

            _lifetime = new CancellationTokenSource(lifetime);
            _lifetime.Token.Register(() => Kill());
        }

        public IReadOnlyList<GethAnswer> Query(IReadOnlyList<KeyValuePair<string, byte[]>> batch)
        {
            var writer = Task.Run(() =>
            {
                for (var i = 0; i < batch.Count; i++)
                {
                    var line = "{\"id\":" + i + ",\"kind\":\"" + batch[i].Key + "\",\"hex\":\"" + Convert.ToHexString(batch[i].Value).ToLowerInvariant() + "\"}";
                    _process.StandardInput.WriteLine(line);
                }
                _process.StandardInput.Flush();
            });

            var answers = new GethAnswer[batch.Count];
            for (var received = 0; received < batch.Count; received++)
            {
                var line = _process.StandardOutput.ReadLine();
                if (line == null)
                    throw new InvalidOperationException("geth reference decoder exited or was stopped (time or memory cap): " + Stderr());
                using (var document = JsonDocument.Parse(line))
                {
                    var root = document.RootElement;
                    var id = root.GetProperty("id").GetInt32();
                    var answer = new GethAnswer { Ok = root.GetProperty("ok").GetBoolean() };
                    if (root.TryGetProperty("err", out var error)) answer.Error = error.GetString();
                    if (root.TryGetProperty("canonical", out var canonical)) answer.Canonical = canonical.GetString();
                    if (root.TryGetProperty("hash", out var hash)) answer.Hash = hash.GetString();
                    if (root.TryGetProperty("extra", out var extra)) answer.Extra = extra.Clone();
                    answers[id] = answer;
                }
            }
            writer.GetAwaiter().GetResult();

            _process.Refresh();
            if (_process.WorkingSet64 > MemoryCapBytes)
            {
                Kill();
                throw new InvalidOperationException("geth reference decoder exceeded the memory cap of " + MemoryCapBytes);
            }
            return answers;
        }

        public string Stderr()
        {
            lock (_stderr) return _stderr.ToString();
        }

        private void Kill()
        {
            try
            {
                if (!_process.HasExited) _process.Kill(true);
            }
            catch (InvalidOperationException)
            {
            }
        }

        public void Dispose()
        {
            try { _process.StandardInput.Close(); } catch (IOException) { } catch (ObjectDisposedException) { }
            if (!_process.WaitForExit(3000)) Kill();
            _lifetime.Dispose();
            _process.Dispose();
        }
    }
}
