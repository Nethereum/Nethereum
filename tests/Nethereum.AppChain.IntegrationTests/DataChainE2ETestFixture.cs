using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain;
using Nethereum.AppChain.Sequencer;

using AppChainCore = Nethereum.AppChain.AppChain;

namespace Nethereum.AppChain.IntegrationTests
{
    public class AppChainE2ETestFixture : IDisposable
    {
        public string DatabasePath { get; }
        public RocksDbManager? Manager { get; private set; }

        public IBlockStore? BlockStore { get; private set; }
        public ITransactionStore? TransactionStore { get; private set; }
        public IReceiptStore? ReceiptStore { get; private set; }
        public ILogStore? LogStore { get; private set; }
        public IStateStore? StateStore { get; private set; }

        public DevChainNode? L1Node { get; private set; }
        public AppChainCore? AppChain { get; private set; }
        public Sequencer.Sequencer? Sequencer { get; private set; }

        public string SequencerAddress { get; } = "0x12345678901234567890123456789012345678aa";
        public string SequencerPrivateKey { get; } = "0x12345678901234567890123456789012345678901234567890123456789012ab";

        public AppChainE2ETestFixture()
        {
            DatabasePath = Path.Combine(Path.GetTempPath(), $"appchain_e2e_{Guid.NewGuid():N}");
        }

        public async Task InitializeAsync(bool deployCreate2Factory = true)
        {
            var options = new RocksDbStorageOptions { DatabasePath = DatabasePath };
            Manager = new RocksDbManager(options);
            BlockStore = new RocksDbBlockStore(Manager);
            TransactionStore = new RocksDbTransactionStore(Manager, BlockStore);
            ReceiptStore = new RocksDbReceiptStore(Manager, BlockStore);
            LogStore = new RocksDbLogStore(Manager);
            StateStore = new RocksDbStateStore(Manager);

            var l1Config = new DevChainConfig
            {
                ChainId = 1337,
                BlockGasLimit = 30_000_000,
                BaseFee = 0,
                InitialBalance = BigInteger.Parse("100000000000000000000000")
            };
            L1Node = new DevChainNode(l1Config);
            await L1Node.StartAsync(new[] { SequencerAddress });

            var appChainConfig = AppChainConfig.CreateWithName("TestAppChain", 420420);
            appChainConfig.SequencerAddress = SequencerAddress;

            AppChain = new AppChainCore(
                appChainConfig,
                BlockStore,
                TransactionStore,
                ReceiptStore,
                LogStore,
                StateStore);

            var genesisOptions = new GenesisOptions
            {
                PrefundedAddresses = new[] { SequencerAddress },
                PrefundBalance = BigInteger.Parse("10000000000000000000000"),
                DeployCreate2Factory = deployCreate2Factory
            };

            await AppChain.InitializeAsync(genesisOptions);

            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = SequencerAddress,
                SequencerPrivateKey = SequencerPrivateKey,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                Policy = Nethereum.AppChain.Sequencer.PolicyConfig.OpenAccess
            };

            Sequencer = new Sequencer.Sequencer(AppChain, sequencerConfig);
        }

        public async Task ResetAsync()
        {
            Sequencer = null;
            AppChain = null;
            L1Node?.Dispose();
            L1Node = null;
            Manager?.Dispose();
            Manager = null;

            if (Directory.Exists(DatabasePath))
            {
                Directory.Delete(DatabasePath, true);
            }
        }

        public void Dispose()
        {
            Sequencer = null;
            AppChain = null;
            L1Node?.Dispose();
            L1Node = null;
            Manager?.Dispose();

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
