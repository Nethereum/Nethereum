using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class GethT8nRunner
    {
        private readonly string _evmExePath;
        private readonly int _timeoutMs;

        public GethT8nRunner(string projectRoot = null, int timeoutMs = 60000)
        {
            _evmExePath = FindEvmExe(projectRoot);
            _timeoutMs = timeoutMs;
        }

        private static string FindEvmExe(string projectRoot)
        {
            if (projectRoot == null)
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                        File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                    { projectRoot = dir.FullName; break; }
                    dir = dir.Parent;
                }
            }
            if (projectRoot == null) throw new DirectoryNotFoundException("project root");
            var gethToolsDir = Path.Combine(projectRoot, "geth-tools");
            var evms = Directory.GetFiles(gethToolsDir, "evm.exe", SearchOption.AllDirectories);
            if (evms.Length == 0) throw new FileNotFoundException("evm.exe not found");
            return evms[0];
        }

        public async Task<GethT8nResult> RunT8nAsync(string fixturePath, int dataIndex, int gasIndex, int valueIndex, string fork)
        {
            var result = new GethT8nResult { FixturePath = fixturePath };
            JObject fixture;
            try
            {
                fixture = JObject.Parse(File.ReadAllText(fixturePath));
            }
            catch (Exception ex)
            {
                result.Error = "Fixture parse: " + ex.Message;
                return result;
            }

            JProperty testEntry = null;
            foreach (var p in fixture.Properties()) { testEntry = p; break; }
            if (testEntry == null) { result.Error = "no test entry"; return result; }
            var test = (JObject)testEntry.Value;

            var alloc = (JObject)test["pre"];
            var env = BuildEnv((JObject)test["env"]);
            var tx = BuildTx((JObject)test["transaction"], dataIndex, gasIndex, valueIndex);

            var tmpDir = Path.Combine(Path.GetTempPath(), "neth_t8n_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tmpDir);
            try
            {
                File.WriteAllText(Path.Combine(tmpDir, "alloc.json"), alloc.ToString(Formatting.None));
                File.WriteAllText(Path.Combine(tmpDir, "env.json"), env.ToString(Formatting.None));
                var txsContent = new JArray(tx).ToString(Formatting.None);
                File.WriteAllText(Path.Combine(tmpDir, "txs.json"), txsContent);
                result.TxsFileContent = txsContent;

                var outDir = Path.Combine(tmpDir, "out");
                Directory.CreateDirectory(outDir);
                var args = $"t8n --state.fork={fork} --input.alloc={Path.Combine(tmpDir, "alloc.json")} --input.env={Path.Combine(tmpDir, "env.json")} --input.txs={Path.Combine(tmpDir, "txs.json")} --output.basedir={outDir} --output.alloc=alloc.json --output.result=result.json";

                var psi = new ProcessStartInfo
                {
                    FileName = _evmExePath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = tmpDir
                };
                using var process = new Process { StartInfo = psi };
                process.Start();
                ChildProcessReaper.Register(process);
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                var completed = await Task.Run(() => process.WaitForExit(_timeoutMs));
                if (!completed) { try { process.Kill(); } catch { } result.Error = "t8n timeout"; return result; }
                var stderr = await stderrTask;
                var stdout = await stdoutTask;
                if (process.ExitCode != 0)
                {
                    result.Error = $"t8n exit {process.ExitCode}: {stderr}";
                    return result;
                }

                var outAllocPath = Path.Combine(outDir, "alloc.json");
                if (!File.Exists(outAllocPath)) { result.Error = "output alloc.json missing"; return result; }
                var outAlloc = JObject.Parse(File.ReadAllText(outAllocPath));
                var resultPath = Path.Combine(outDir, "result.json");
                if (File.Exists(resultPath))
                {
                    var resultObj = JObject.Parse(File.ReadAllText(resultPath));
                    var rejected = resultObj["rejected"] as JArray;
                    if (rejected != null && rejected.Count > 0)
                    {
                        result.Error = $"tx rejected: {rejected[0]}";
                        return result;
                    }
                    result.StateRoot = (string)resultObj["stateRoot"];
                    result.GasUsed = (string)resultObj["gasUsed"];
                }

                result.PostState = new Dictionary<string, GethPostStateAccount>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in outAlloc.Properties())
                {
                    var addr = p.Name.ToLowerInvariant();
                    var v = (JObject)p.Value;
                    var acc = new GethPostStateAccount
                    {
                        Balance = (string)v["balance"] ?? "0x0",
                        Nonce = (string)v["nonce"] ?? "0x0",
                        Code = (string)v["code"] ?? "0x"
                    };
                    var storage = v["storage"] as JObject;
                    if (storage != null)
                    {
                        foreach (var s in storage.Properties())
                            acc.Storage[s.Name] = (string)s.Value;
                    }
                    result.PostState[addr] = acc;
                }
                result.Success = true;
                return result;
            }
            finally
            {
                if (result.Success)
                {
                    try { Directory.Delete(tmpDir, recursive: true); } catch { }
                }
                else
                {
                    result.Error = (result.Error ?? "") + " | tmp=" + tmpDir;
                }
            }
        }

        private static JObject BuildEnv(JObject src)
        {
            var env = new JObject();
            foreach (var p in src.Properties()) env[p.Name] = p.Value;
            if (env["withdrawals"] == null) env["withdrawals"] = new JArray();
            if (env["parentBeaconBlockRoot"] == null) env["parentBeaconBlockRoot"] = "0x0000000000000000000000000000000000000000000000000000000000000000";
            return env;
        }

        private static string CanonicalHex(string s)
        {
            var bi = new HexBigInteger(s).Value;
            if (bi.IsZero) return "0x0";
            var hex = bi.ToString("x").TrimStart('0');
            return hex.Length == 0 ? "0x0" : "0x" + hex;
        }

        private static JObject BuildTx(JObject src, int dataIndex, int gasIndex, int valueIndex)
        {
            var tx = new JObject();
            var data = (JArray)src["data"];
            var gasLimit = (JArray)src["gasLimit"];
            var value = (JArray)src["value"];
            tx["input"] = (string)data[dataIndex];
            tx["gas"] = CanonicalHex((string)gasLimit[gasIndex]);
            tx["value"] = CanonicalHex((string)value[valueIndex]);
            tx["nonce"] = CanonicalHex((string)src["nonce"] ?? "0x0");
            tx["gasPrice"] = CanonicalHex((string)src["gasPrice"] ?? "0x1");
            tx["to"] = src["to"] ?? "";
            tx["secretKey"] = src["secretKey"];
            tx["v"] = "0x0"; tx["r"] = "0x0"; tx["s"] = "0x0";
            return tx;
        }
    }

    public class GethT8nResult
    {
        public string FixturePath { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public string StateRoot { get; set; }
        public string GasUsed { get; set; }
        public string TxsFileContent { get; set; }
        public Dictionary<string, GethPostStateAccount> PostState { get; set; }
    }

    public class GethPostStateAccount
    {
        public string Balance { get; set; } = "0x0";
        public string Nonce { get; set; } = "0x0";
        public string Code { get; set; } = "0x";
        public Dictionary<string, string> Storage { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
