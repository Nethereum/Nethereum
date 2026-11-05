using Nethereum.EVM;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.EEST.Conformance.Diagnosis
{
    public class GethT8nRunner
    {
        private readonly string _evmExePath;
        private readonly int _timeoutMs;

        public GethT8nRunner(string evmPath, int timeoutMs = 60000)
        {
            _evmExePath = evmPath;
            if (!File.Exists(_evmExePath))
                throw new FileNotFoundException(
                    $"Tools:Evm not found at '{_evmExePath}'; run dotnet run ci/fetch-externals.cs or set Tools:Evm in the local test settings.");
            _timeoutMs = timeoutMs;
        }

        public async Task<GethT8nResult> RunT8nAsync(string fixturePath, int dataIndex, int gasIndex, int valueIndex, string fork, string testName = null)
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
            foreach (var p in fixture.Properties())
            {
                if (testName == null || p.Name == testName) { testEntry = p; break; }
            }
            if (testEntry == null) { result.Error = "no test entry"; return result; }
            var test = (JObject)testEntry.Value;

            var alloc = (JObject)test["pre"];
            var env = BuildEnv((JObject)test["env"], fork);
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

        private static JObject BuildEnv(JObject src, string fork)
        {
            var env = new JObject();
            foreach (var p in src.Properties()) env[p.Name] = p.Value;
            var hardfork = HardforkNames.Parse(fork);
            if (hardfork < HardforkName.Cancun)
            {
                foreach (var blobField in new[] { "currentExcessBlobGas", "currentBlobGasUsed", "parentExcessBlobGas", "parentBlobGasUsed", "parentBeaconBlockRoot" })
                    env.Remove(blobField);
            }
            else if (env["parentBeaconBlockRoot"] == null)
            {
                env["parentBeaconBlockRoot"] = "0x0000000000000000000000000000000000000000000000000000000000000000";
            }
            if (hardfork >= HardforkName.Shanghai && env["withdrawals"] == null) env["withdrawals"] = new JArray();
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
            var to = (string)src["to"];
            if (!string.IsNullOrEmpty(to)) tx["to"] = to;
            tx["secretKey"] = src["secretKey"];
            tx["v"] = "0x0"; tx["r"] = "0x0"; tx["s"] = "0x0";

            var accessList = src["accessLists"] is JArray accessLists && dataIndex < accessLists.Count && accessLists[dataIndex].Type != JTokenType.Null
                ? accessLists[dataIndex]
                : null;

            if (src["maxFeePerGas"] != null)
            {
                tx["type"] = src["authorizationList"] != null ? "0x4" : src["blobVersionedHashes"] != null ? "0x3" : "0x2";
                tx["chainId"] = "0x1";
                tx["maxFeePerGas"] = CanonicalHex((string)src["maxFeePerGas"]);
                tx["maxPriorityFeePerGas"] = CanonicalHex((string)src["maxPriorityFeePerGas"] ?? "0x0");
                tx["accessList"] = accessList ?? new JArray();
                if (src["blobVersionedHashes"] != null)
                {
                    tx["blobVersionedHashes"] = src["blobVersionedHashes"];
                    tx["maxFeePerBlobGas"] = CanonicalHex((string)src["maxFeePerBlobGas"] ?? "0x0");
                }
                if (src["authorizationList"] != null) tx["authorizationList"] = src["authorizationList"];
            }
            else if (accessList != null)
            {
                tx["type"] = "0x1";
                tx["chainId"] = "0x1";
                tx["gasPrice"] = CanonicalHex((string)src["gasPrice"] ?? "0x1");
                tx["accessList"] = accessList;
            }
            else
            {
                tx["gasPrice"] = CanonicalHex((string)src["gasPrice"] ?? "0x1");
            }
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
