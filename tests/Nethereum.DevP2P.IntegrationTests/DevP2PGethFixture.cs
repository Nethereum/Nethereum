using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Geth;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Accounts;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class DevP2PGethFixture : IAsyncLifetime
    {
        public const string COLLECTION_NAME = "DevP2P Geth Cluster";

        public const string SealerAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
        public const string SealerPrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        public const int P2PPort = 30330;
        public const int RpcPort = 8555;
        public static readonly BigInteger ChainId = 1337;

        private const string GethRelativePath =
            @"..\..\..\..\..\geth-tools\geth-windows-amd64-1.13.15-c5ba367e\geth.exe";
        private const string GenesisRelativePath =
            @"..\..\..\..\..\testchain\clique\genesis_clique_modern.json";
        private const string KeystoreSourceRelative =
            @"..\..\..\..\..\testchain\clique\devChain\keystore";
        private const string PasswordRelative =
            @"..\..\..\..\..\testchain\clique\pass.txt";

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
            return await PollForReceiptAsync(web3, txHash, TimeSpan.FromSeconds(15));
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
                    // Geth is still indexing — retry
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
            var assemblyDir = Path.GetDirectoryName(
                typeof(DevP2PGethFixture).GetTypeInfo().Assembly.Location);
            GethBinaryPath = Path.GetFullPath(Path.Combine(assemblyDir, GethRelativePath));

            if (!File.Exists(GethBinaryPath))
                throw new FileNotFoundException(
                    $"Geth 1.13.15 binary not found at {GethBinaryPath}. " +
                    "Run the DevP2P setup to download it.");

            _dataDir = Path.Combine(
                Path.GetTempPath(),
                $"nethereum-devp2p-{P2PPort}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_dataDir);

            var assemblyDirInfo = new DirectoryInfo(assemblyDir);
            var genesisPath = Path.GetFullPath(Path.Combine(assemblyDir, GenesisRelativePath));
            var keystoreSource = Path.GetFullPath(Path.Combine(assemblyDir, KeystoreSourceRelative));
            var passwordSource = Path.GetFullPath(Path.Combine(assemblyDir, PasswordRelative));

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

            await WaitForRpcReadyAsync(TimeSpan.FromSeconds(20));
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
