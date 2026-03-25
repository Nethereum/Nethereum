using System.Diagnostics;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Nethereum.PrivacyPools.CrossSdk.Tests
{
    public class NodeRunnerResult
    {
        public int ExitCode { get; set; }
        public string Stdout { get; set; } = "";
        public string Stderr { get; set; } = "";
        public bool Success => ExitCode == 0;

        public JObject ParseJson()
        {
            var trimmed = Stdout.Trim();
            var lastLine = trimmed.Contains('\n')
                ? trimmed[(trimmed.LastIndexOf('\n') + 1)..]
                : trimmed;
            return JObject.Parse(lastLine);
        }
    }

    public static class NodeRunner
    {
        public static async Task<NodeRunnerResult> RunAsync(
            string scriptsDir, string scriptName, object input,
            int timeoutMs = 120_000)
        {
            var inputFile = Path.Combine(Path.GetTempPath(),
                $"crosssdk_{Guid.NewGuid():N}.json");

            try
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(input);
                await File.WriteAllTextAsync(inputFile, json);

                var scriptPath = Path.Combine(scriptsDir, scriptName);
                var nodePath = FindNode();

                var psi = new ProcessStartInfo
                {
                    FileName = nodePath,
                    Arguments = $"\"{scriptPath}\" \"{inputFile}\"",
                    WorkingDirectory = scriptsDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) stdout.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) stderr.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using var cts = new CancellationTokenSource(timeoutMs);
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(true); } catch { }
                    throw new TimeoutException(
                        $"Node script {scriptName} timed out after {timeoutMs}ms. Stderr: {stderr}");
                }
                finally
                {
                    try { process.CancelOutputRead(); } catch { }
                    try { process.CancelErrorRead(); } catch { }
                }

                return new NodeRunnerResult
                {
                    ExitCode = process.ExitCode,
                    Stdout = stdout.ToString(),
                    Stderr = stderr.ToString()
                };
            }
            finally
            {
                try { File.Delete(inputFile); } catch { }
            }
        }

        private static string FindNode()
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
            var extensions = OperatingSystem.IsWindows()
                ? new[] { ".exe", ".cmd" }
                : new[] { "" };

            foreach (var dir in pathVar.Split(Path.PathSeparator))
            {
                foreach (var ext in extensions)
                {
                    var candidate = Path.Combine(dir, $"node{ext}");
                    if (File.Exists(candidate)) return candidate;
                }
            }

            return "node";
        }
    }
}
