using Nethereum.DevP2P.IntegrationTests.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Geth;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Testing;
using Nethereum.Web3.Accounts;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class DevP2PGethFixture : IAsyncLifetime
    {
        public const string COLLECTION_NAME = "DevP2P Geth Cluster";

        public const string SealerAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
        public const string SealerPrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        public static readonly BigInteger ChainId = 1337;

        private static readonly TimeSpan RpcReadyTimeout = TimeSpan.FromSeconds(60);

        public int P2PPort { get; } = FreeTcpPort();
        public int RpcPort { get; } = FreeTcpPort();

        private static int FreeTcpPort()
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static readonly string CliqueDir = Path.Combine(TestEnvironment.RepoRoot, "testchain", "clique");

        private Process _process;
        private string _dataDir;

        public string GethBinaryPath { get; private set; }
        public string Enode { get; private set; }
        public byte[] GenesisHash { get; private set; }
        public byte[] GenesisStateRoot { get; private set; }
        public ulong NetworkId { get; private set; }

        public IClient GetClient() => new RpcClient(new Uri($"http://127.0.0.1:{RpcPort}"));

        public Web3Geth GetWeb3() => new Web3Geth(GetClient());

        public Nethereum.Web3.Web3 GetWeb3WithSealerAccount()
        {
            var account = new Account(SealerPrivateKey, ChainId);
            return new Nethereum.Web3.Web3(account, $"http://127.0.0.1:{RpcPort}");
        }

        public async Task<TransactionReceipt> SendEtherFromSealerAsync(string toAddress, BigInteger weiAmount)
        {
            var web3 = GetWeb3WithSealerAccount();
            var input = new TransactionInput
            {
                From = SealerAddress,
                To = toAddress,
                Value = new HexBigInteger(weiAmount),
                Gas = new HexBigInteger(21000)
            };

            var txHash = await web3.Eth.TransactionManager.SendTransactionAsync(input);
            var receipt = await PollForReceiptAsync(web3, txHash, TimeSpan.FromSeconds(15));
            await WaitForCanonicalHeadAsync(web3, receipt.BlockNumber.Value, TimeSpan.FromSeconds(15));
            return receipt;
        }

        private static async Task WaitForCanonicalHeadAsync(
            Nethereum.Web3.Web3 web3, BigInteger blockNumber, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var head = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                if (head.Value >= blockNumber) return;
                await Task.Delay(100);
            }
            throw new TimeoutException(
                $"Geth's canonical head did not reach block {blockNumber} within {timeout.TotalSeconds}s");
        }

        private static async Task<TransactionReceipt> PollForReceiptAsync(
            Nethereum.Web3.Web3 web3, string txHash, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
                    if (receipt != null) return receipt;
                }
                catch (RpcResponseException ex) when (ex.Message.Contains("transaction indexing is in progress"))
                {
                }
                await Task.Delay(250);
            }
            throw new TimeoutException(
                $"Tx {txHash} did not get a receipt within {timeout.TotalSeconds}s");
        }

        public async Task InitializeAsync()
        {
            ResolvePaths();
            await StartGethAsync();
            await PopulateChainMetadataAsync();
        }

        public async Task DisposeAsync()
        {
            if (_process != null && !_process.HasExited)
            {
                try
                {
                    _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync();
                }
                catch { }
                _process.Dispose();
            }

            if (!string.IsNullOrEmpty(_dataDir) && Directory.Exists(_dataDir))
            {
                try { Directory.Delete(_dataDir, true); }
                catch { }
            }
        }

        private void ResolvePaths()
        {
            GethBinaryPath = GethToolLocator.FindGethBinary();

            _dataDir = Path.Combine(
                Path.GetTempPath(),
                $"nethereum-devp2p-{P2PPort}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_dataDir);

            var genesisPath = Path.Combine(CliqueDir, "genesis_clique_modern.json");
            var keystoreSource = Path.Combine(CliqueDir, "devChain", "keystore");
            var passwordSource = Path.Combine(CliqueDir, "pass.txt");

            RunGeth($"--datadir=\"{_dataDir}\" init \"{genesisPath}\"");

            var keystoreDest = Path.Combine(_dataDir, "keystore");
            Directory.CreateDirectory(keystoreDest);
            foreach (var file in Directory.GetFiles(keystoreSource))
                File.Copy(file, Path.Combine(keystoreDest, Path.GetFileName(file)), overwrite: true);
            File.Copy(passwordSource, Path.Combine(_dataDir, "pass.txt"), overwrite: true);
        }

        private void RunGeth(string args)
        {
            var psi = new ProcessStartInfo(GethBinaryPath, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var p = Process.Start(psi);
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                var stderr = p.StandardError.ReadToEnd();
                throw new InvalidOperationException(
                    $"geth {args} failed with exit code {p.ExitCode}: {stderr}");
            }
        }

        private async Task StartGethAsync()
        {
            var args = string.Join(" ",
                "--nodiscover",
                "--ipcdisable",
                "--port", P2PPort.ToString(),
                "--http",
                "--http.port", RpcPort.ToString(),
                "--http.addr", "127.0.0.1",
                "--http.api", "eth,web3,net,admin,miner,debug,personal",
                "--datadir", $"\"{_dataDir}\"",
                "--mine",
                "--miner.etherbase", SealerAddress,
                "--unlock", SealerAddress,
                "--password", $"\"{Path.Combine(_dataDir, "pass.txt")}\"",
                "--allow-insecure-unlock",
                "--verbosity", "1");

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GethBinaryPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                }
            };
            _process.Start();
            _process.BeginErrorReadLine();

            await WaitForRpcReadyAsync(RpcReadyTimeout);
        }

        private async Task WaitForRpcReadyAsync(TimeSpan timeout)
        {
            var web3 = GetWeb3();
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (_process.HasExited)
                    throw new InvalidOperationException(
                        $"Geth exited with code {_process.ExitCode} before RPC became ready");
                try
                {
                    await web3.Net.Version.SendRequestAsync();
                    return;
                }
                catch
                {
                    await Task.Delay(250);
                }
            }
            throw new TimeoutException(
                $"Geth RPC at port {RpcPort} did not become ready within {timeout.TotalSeconds}s");
        }

        private async Task PopulateChainMetadataAsync()
        {
            var web3 = GetWeb3();

            NetworkId = ulong.Parse(await web3.Net.Version.SendRequestAsync());

            var nodeInfo = await web3.Admin.NodeInfo.SendRequestAsync();
            Enode = nodeInfo["enode"].ToString();

            var genesis = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(new BlockParameter(0));
            GenesisHash = genesis.BlockHash.HexToByteArray();
            GenesisStateRoot = genesis.StateRoot.HexToByteArray();
        }
    }

    [CollectionDefinition(DevP2PGethFixture.COLLECTION_NAME)]
    public class DevP2PGethCollection : ICollectionFixture<DevP2PGethFixture>
    {
    }
}
