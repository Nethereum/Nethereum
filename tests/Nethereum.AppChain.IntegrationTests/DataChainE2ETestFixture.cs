using System;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain;
using Nethereum.Signer;

using AppChainCore = Nethereum.AppChain.AppChain;

namespace Nethereum.AppChain.IntegrationTests
{
    public class AppChainE2ETestFixture : IDisposable
    {
        public string DatabasePath { get; }
        public RocksDbManager? Manager => _composed?.ChainNode.Storage.Manager;

        public IBlockStore? BlockStore => _composed?.Bundle.Blocks;
        public ITransactionStore? TransactionStore => _composed?.Bundle.Transactions;
        public IReceiptStore? ReceiptStore => _composed?.Bundle.Receipts;
        public ILogStore? LogStore => _composed?.Bundle.Logs;
        public IStateStore? StateStore => _composed?.Bundle.State;

        public DevChainNode? L1Node { get; private set; }
        public AppChainCore? AppChain => _composed?.AppChain;
        public ISequencer? Sequencer => _composed?.Sequencer;

        public string SequencerAddress { get; } = "0x12345678901234567890123456789012345678aa";
        public string SequencerPrivateKey { get; } = "0x12345678901234567890123456789012345678901234567890123456789012ab";

        private AppChainComposedNode? _composed;
        private bool _signRecoverableBeforeCompose;

        public AppChainE2ETestFixture()
        {
            DatabasePath = Path.Combine(Path.GetTempPath(), $"appchain_e2e_{Guid.NewGuid():N}");
        }

        public async Task InitializeAsync(bool deployCreate2Factory = true)
        {
            var l1Config = new DevChainConfig
            {
                ChainId = 1337,
                BlockGasLimit = 30_000_000,
                BaseFee = 0,
                InitialBalance = BigInteger.Parse("100000000000000000000000")
            };
            L1Node = new DevChainNode(l1Config);
            await L1Node.StartAsync(new[] { SequencerAddress });

            var config = new AppChainServerConfig
            {
                ChainId = 420420,
                ChainName = "TestAppChain"
            };
            config.Genesis.Owner.PrivateKey = SequencerPrivateKey;
            config.Genesis.DeployCreate2Factory = deployCreate2Factory;
            config.Consensus.Sequencer.Address = SequencerAddress;
            config.Consensus.Sequencer.PrivateKey = SequencerPrivateKey;
            config.Consensus.BlockProductionMode = BlockProductionMode.OnDemand;
            config.Consensus.BlockTimeMs = 0;
            config.Node.Storage.InMemory = false;
            config.Node.Storage.DataDirectory = DatabasePath;
            config.Node.Network.Serve = false;
            config.Node.Sync.Mode = SyncMode.None;
            config.Mud.DeployWorld = false;

            _signRecoverableBeforeCompose = EthECKey.SignRecoverable;
            _composed = await AppChainComposition.ComposeAsync(config, NullLoggerFactory.Instance, CancellationToken.None);
        }

        public async Task ResetAsync()
        {
            if (_composed != null)
            {
                await _composed.DisposeAsync();
                _composed = null;
                EthECKey.SignRecoverable = _signRecoverableBeforeCompose;
            }
            L1Node?.Dispose();
            L1Node = null;

            if (Directory.Exists(DatabasePath))
            {
                Directory.Delete(DatabasePath, true);
            }
        }

        public void Dispose()
        {
            if (_composed != null)
            {
                _composed.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _composed = null;
                EthECKey.SignRecoverable = _signRecoverableBeforeCompose;
            }
            L1Node?.Dispose();
            L1Node = null;

            if (Directory.Exists(DatabasePath))
            {
                try
                {
                    Directory.Delete(DatabasePath, true);
                }
                catch
                {
                }
            }
        }
    }
}
