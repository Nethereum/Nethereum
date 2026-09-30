using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Nethereum.RPC.Extensions;

namespace Nethereum.XUnitEthereumClients
{
    public class StrictBundlerFixture : IDisposable
    {
        public const string COLLECTION_NAME = "StrictBundler";
        private const int PORT = 3100;

        public const string EntryPointAddress = "0x4337084D9E255Ff0702461CF8895CE9E3b5Ff108";

        public bool IsAvailable { get; }
        public string UnavailableReason { get; }
        public string BundlerUrl { get; } = $"http://localhost:{PORT}/rpc";
        public string BundlerRepoPath { get; }
        public AnvilMainnetForkFixture Fork { get; }

        private Process _process;
        private readonly string _logPath;

        public StrictBundlerFixture()
        {
            var settings = new EthereumClientIntegrationFixture.EthereumTestSettings();
            var config = EthereumClientIntegrationFixture.InitConfiguration();
            config?.GetSection("EthereumTestSettings").Bind(settings);

            if (string.IsNullOrEmpty(settings.StrictBundlerPath))
            {
                IsAvailable = false;
                UnavailableReason =
                    "No strict bundler configured. Set EthereumTestSettings.StrictBundlerPath in " +
                    "appsettings.test.local.json to a yarn-installed checkout of " +
                    "github.com/eth-infinitism/bundler and rebuild.";
                return;
            }

            BundlerRepoPath = settings.StrictBundlerPath;
            Fork = new AnvilMainnetForkFixture();
            if (!Fork.IsAvailable)
            {
                IsAvailable = false;
                UnavailableReason = Fork.UnavailableReason;
                return;
            }

            const string BENEFICIARY = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
            var web3 = Fork.GetWeb3();
            web3.Eth.Anvil().SetBalance.SendRequestAsync(BENEFICIARY,
                new Nethereum.Hex.HexTypes.HexBigInteger(Nethereum.Web3.Web3.Convert.ToWei(10000))).Wait();

            var mnemonicPath = Path.Combine(settings.StrictBundlerPath,
                "packages", "bundler", "localconfig", "mnemonic.txt");
            if (!File.Exists(mnemonicPath))
            {
                File.WriteAllText(mnemonicPath,
                    "test test test test test test test test test test test junk");
            }

            var configPath = Path.Combine(settings.StrictBundlerPath,
                "packages", "bundler", "localconfig", "bundler.nethereum.config.json");
            File.WriteAllText(configPath, $@"{{
  ""chainId"": {Fork.ChainId},
  ""gasFactor"": ""1"",
  ""port"": ""{PORT}"",
  ""privateApiPort"": ""3101"",
  ""network"": ""{Fork.RpcUrl}"",
  ""entryPoint"": ""{EntryPointAddress}"",
  ""beneficiary"": ""{BENEFICIARY}"",
  ""minBalance"": ""1"",
  ""mnemonic"": ""./localconfig/mnemonic.txt"",
  ""maxBundleGas"": 5000000,
  ""minStake"": ""1"",
  ""minUnstakeDelay"": 0,
  ""autoBundleInterval"": 2,
  ""autoBundleMempoolSize"": 10,
  ""minLogBlock"": {Fork.ForkBlockNumber}
}}");

            _logPath = Path.Combine(Path.GetTempPath(), $"strict-bundler-{PORT}.log");
            try
            {
                _process = Process.Start(new ProcessStartInfo("cmd.exe",
                    $"/c yarn run bundler --unsafe --config ./localconfig/bundler.nethereum.config.json > \"{_logPath}\" 2>&1")
                {
                    WorkingDirectory = settings.StrictBundlerPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (_process == null)
                    throw new InvalidOperationException("Process.Start returned no process for the bundler.");
            }
            catch (Exception)
            {
                Dispose();
                throw;
            }

            try
            {
                WaitForBundlerReady();
            }
            catch
            {
                Dispose();
                throw;
            }

            IsAvailable = true;
        }

        private void WaitForBundlerReady()
        {
            var bundler = new Nethereum.RPC.AccountAbstractionBundlerService(
                new Nethereum.JsonRpc.Client.RpcClient(new Uri(BundlerUrl)));
            var deadline = DateTime.UtcNow.AddSeconds(90);
            var lastError = "no response";
            while (DateTime.UtcNow < deadline)
            {
                if (_process.HasExited)
                    throw new InvalidOperationException(
                        $"The bundler exited with code {_process.ExitCode}. Log tail: {ReadLogTail()}");
                try
                {
                    var entryPoints = bundler.SupportedEntryPoints.SendRequestAsync().Result;
                    if (entryPoints != null && Array.Exists(entryPoints,
                            e => EntryPointAddress.Equals(e, StringComparison.OrdinalIgnoreCase)))
                        return;
                    lastError = "eth_supportedEntryPoints returned [" +
                        string.Join(", ", entryPoints ?? Array.Empty<string>()) + "]";
                }
                catch (Exception ex)
                {
                    lastError = ex.GetBaseException().Message;
                }
                Thread.Sleep(1000);
            }
            throw new InvalidOperationException(
                $"The bundler did not become available at {BundlerUrl} within 90 seconds. " +
                $"Last error: {lastError}. Log tail: {ReadLogTail()}");
        }

        private string ReadLogTail()
        {
            try
            {
                var text = File.ReadAllText(_logPath);
                return text.Length <= 800 ? text : text.Substring(text.Length - 800);
            }
            catch
            {
                return "(log unavailable)";
            }
        }

        public void Dispose()
        {
            if (_process != null)
            {
                try
                {
                    if (!_process.HasExited) _process.Kill(true);
                }
                catch (InvalidOperationException)
                {
                }
                _process.Dispose();
                _process = null;
            }
            Fork?.Dispose();
        }
    }
}
