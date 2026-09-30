using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading;
using Microsoft.Extensions.Configuration;

namespace Nethereum.XUnitEthereumClients
{
    public class AnvilMainnetForkFixture : IDisposable
    {
        public const string COLLECTION_NAME = "AnvilMainnetFork";
        private const int PORT = 8547;
        private const int FORK_BLOCK_SAFETY_MARGIN = 10;

        public const string OperatorPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

        public bool IsAvailable { get; }
        public string UnavailableReason { get; }
        public string RpcUrl { get; } = $"http://127.0.0.1:{PORT}";
        public BigInteger ChainId { get; private set; }
        public BigInteger ForkBlockNumber { get; private set; }

        private Process _process;
        private StreamWriter _logWriter;

        public AnvilMainnetForkFixture()
        {
            var settings = new EthereumClientIntegrationFixture.EthereumTestSettings();
            var config = EthereumClientIntegrationFixture.InitConfiguration();
            config?.GetSection("EthereumTestSettings").Bind(settings);

            if (string.IsNullOrEmpty(settings.AnvilForkUrl))
            {
                IsAvailable = false;
                UnavailableReason =
                    "No fork url configured. Set EthereumTestSettings.AnvilForkUrl in " +
                    "appsettings.test.local.json (kept out of git) to a node rpc url and rebuild " +
                    "to enable the mainnet fork tests.";
                return;
            }

            var forkSource = new Web3.Web3(settings.AnvilForkUrl);
            var tip = forkSource.Eth.Blocks.GetBlockNumber.SendRequestAsync().Result.Value;
            ForkBlockNumber = tip - FORK_BLOCK_SAFETY_MARGIN;

            var logPath = Path.Combine(Path.GetTempPath(), $"anvil-fork-{PORT}.log");
            _logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };

            try
            {
                _process = Process.Start(new ProcessStartInfo("anvil",
                    $"--fork-url {settings.AnvilForkUrl} --fork-block-number {ForkBlockNumber} --port {PORT} --no-rate-limit --block-time 1")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                if (_process == null)
                    throw new InvalidOperationException("Process.Start returned no process for anvil.");

                _process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logWriter.WriteLine($"[OUT] {e.Data}");
                        Debug.WriteLine($"[ANVIL] {e.Data}");
                    }
                };
                _process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logWriter.WriteLine($"[ERR] {e.Data}");
                        Debug.WriteLine($"[ANVIL-ERR] {e.Data}");
                    }
                };
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new InvalidOperationException(
                    "Could not start anvil. Install Foundry (https://getfoundry.sh) so 'anvil' is on the PATH.", ex);
            }

            try
            {
                WaitForOwnedNode();
            }
            catch
            {
                Dispose();
                throw;
            }

            IsAvailable = true;
        }

        public Web3.Web3 GetWeb3()
        {
            return new Web3.Web3(
                new Web3.Accounts.Account(OperatorPrivateKey, ChainId), RpcUrl);
        }

        private void WaitForOwnedNode()
        {
            var web3 = new Web3.Web3(RpcUrl);
            var deadline = DateTime.UtcNow.AddSeconds(30);
            Exception lastError = null;

            while (DateTime.UtcNow < deadline)
            {
                if (_process.HasExited)
                    throw new InvalidOperationException(
                        $"anvil exited with code {_process.ExitCode} while forking. " +
                        "Check the fork url is reachable and port 8547 is free.");
                try
                {
                    ChainId = web3.Eth.ChainId.SendRequestAsync().Result.Value;
                    var currentBlock = web3.Eth.Blocks.GetBlockNumber.SendRequestAsync().Result.Value;

                    var blockDrift = currentBlock - ForkBlockNumber;
                    if (blockDrift < 0 || blockDrift > 200)
                        throw new InvalidOperationException(
                            $"The node at {RpcUrl} is at block {currentBlock}, not near the fork block {ForkBlockNumber} " +
                            $"(drift: {blockDrift}) — likely a stale process holding the port.");

                    return;
                }
                catch (Exception ex) when (!(ex is InvalidOperationException))
                {
                    lastError = ex.GetBaseException();
                }
                Thread.Sleep(500);
            }

            throw new InvalidOperationException(
                $"anvil did not become available at {RpcUrl} within 30 seconds.", lastError);
        }

        public void Dispose()
        {
            if (_process == null) return;
            try
            {
                if (!_process.HasExited) _process.Kill(true);
                _process.WaitForExit(5000);
                _process.CancelOutputRead();
                _process.CancelErrorRead();
            }
            catch (InvalidOperationException)
            {
            }
            _process.Dispose();
            _process = null;
            _logWriter?.Dispose();
        }
    }
}
