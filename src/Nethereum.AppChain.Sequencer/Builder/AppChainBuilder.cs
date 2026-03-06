using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.RPC.Accounts;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

namespace Nethereum.AppChain.Sequencer.Builder
{
    public class AppChainBuilder
    {
        private readonly string _name;
        private readonly BigInteger _chainId;

        private IAccount? _operatorAccount;
        private string? _operatorAddress;
        private string? _operatorPrivateKey;

        private StorageConfig _storageConfig = StorageConfig.InMemory();
        private AAConfig? _aaConfig;
        private TrustConfig? _trustConfig;
        private MudConfig? _mudConfig;
        private OperatorRecoveryConfig? _recoveryConfig;

        private ulong _baseFee = 1_000_000_000;
        private ulong _blockGasLimit = 30_000_000;
        private int _blockTimeMs = 0;
        private BlockProductionMode _blockProductionMode = BlockProductionMode.OnDemand;
        private int _maxTransactionsPerBlock = 1000;

        private List<string>? _prefundedAddresses;
        private BigInteger? _prefundBalance;

        public AppChainBuilder(string name, int chainId)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _chainId = chainId;
        }

        public AppChainBuilder(string name, BigInteger chainId)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _chainId = chainId;
        }

        public AppChainBuilder WithOperator(IAccount account)
        {
            _operatorAccount = account ?? throw new ArgumentNullException(nameof(account));
            _operatorAddress = account.Address;
            return this;
        }

        public AppChainBuilder WithOperator(string privateKey)
        {
            if (string.IsNullOrEmpty(privateKey))
                throw new ArgumentException("Private key cannot be null or empty", nameof(privateKey));

            _operatorPrivateKey = privateKey;
            _operatorAccount = new Account(privateKey, (int)_chainId);
            _operatorAddress = _operatorAccount.Address;
            return this;
        }

        public AppChainBuilder WithOperatorAddress(string address)
        {
            _operatorAddress = address ?? throw new ArgumentNullException(nameof(address));
            return this;
        }

        public AppChainBuilder WithStorage(StorageConfig config)
        {
            _storageConfig = config ?? throw new ArgumentNullException(nameof(config));
            return this;
        }

        public AppChainBuilder WithStorage(StorageType type, string? path = null)
        {
            _storageConfig = type switch
            {
                StorageType.InMemory => StorageConfig.InMemory(),
                StorageType.RocksDb => StorageConfig.RocksDb(path ?? $"./data/{_name}"),
                _ => throw new ArgumentException($"Unknown storage type: {type}")
            };
            return this;
        }

        public AppChainBuilder WithAA(PaymasterType paymaster = PaymasterType.None)
        {
            _aaConfig = new AAConfig
            {
                Enabled = true,
                Paymaster = paymaster,
                AutoDeployFactory = true
            };
            return this;
        }

        public AppChainBuilder WithAA(AAConfig config)
        {
            _aaConfig = config ?? throw new ArgumentNullException(nameof(config));
            return this;
        }

        public AppChainBuilder WithTrust(TrustModel model)
        {
            _trustConfig = model switch
            {
                TrustModel.Open => TrustConfig.Open(),
                TrustModel.Quota => TrustConfig.QuotaBased(),
                _ => new TrustConfig { Model = model }
            };
            return this;
        }

        public AppChainBuilder WithTrust(TrustModel model, int maxInvites)
        {
            if (model != TrustModel.InviteTree)
                throw new ArgumentException("maxInvites parameter only applies to InviteTree model");

            _trustConfig = TrustConfig.InviteTree(maxInvites);
            return this;
        }

        public AppChainBuilder WithTrust(TrustModel model, string admin, IReadOnlyList<string>? allowed = null)
        {
            if (model != TrustModel.Whitelist)
                throw new ArgumentException("admin parameter only applies to Whitelist model");

            _trustConfig = TrustConfig.Whitelist(admin, allowed);
            return this;
        }

        public AppChainBuilder WithTrust(TrustConfig config)
        {
            _trustConfig = config ?? throw new ArgumentNullException(nameof(config));
            return this;
        }

        public AppChainBuilder WithMUD()
        {
            _mudConfig = MudConfig.Default();
            return this;
        }

        public AppChainBuilder WithMUD(MudConfig config)
        {
            _mudConfig = config ?? throw new ArgumentNullException(nameof(config));
            return this;
        }

        public AppChainBuilder WithOperatorRecovery(OperatorRecoveryConfig config)
        {
            _recoveryConfig = config ?? throw new ArgumentNullException(nameof(config));
            return this;
        }

        public AppChainBuilder WithBaseFee(ulong baseFee)
        {
            _baseFee = baseFee;
            return this;
        }

        public AppChainBuilder WithBlockGasLimit(ulong limit)
        {
            _blockGasLimit = limit;
            return this;
        }

        public AppChainBuilder WithBlockTime(int milliseconds)
        {
            _blockTimeMs = milliseconds;
            _blockProductionMode = milliseconds > 0 ? BlockProductionMode.Interval : BlockProductionMode.OnDemand;
            return this;
        }

        public AppChainBuilder WithOnDemandBlocks()
        {
            _blockTimeMs = 0;
            _blockProductionMode = BlockProductionMode.OnDemand;
            return this;
        }

        public AppChainBuilder WithMaxTransactionsPerBlock(int max)
        {
            _maxTransactionsPerBlock = max;
            return this;
        }

        public AppChainBuilder WithPrefundedAddresses(IEnumerable<string> addresses, BigInteger? balance = null)
        {
            _prefundedAddresses = new List<string>(addresses);
            _prefundBalance = balance;
            return this;
        }

        public async Task<AppChainInstance> BuildAsync()
        {
            if (string.IsNullOrEmpty(_operatorAddress))
                throw new InvalidOperationException("Operator must be configured using WithOperator()");

            var (blockStore, transactionStore, receiptStore, logStore, stateStore, trieNodeStore) = CreateStores();

            var appChainConfig = new AppChainConfig
            {
                AppChainName = _name,
                ChainId = _chainId,
                SequencerAddress = _operatorAddress,
                BaseFee = _baseFee,
                BlockGasLimit = _blockGasLimit
            };

            var appChain = new Nethereum.AppChain.AppChain(
                appChainConfig,
                blockStore,
                transactionStore,
                receiptStore,
                logStore,
                stateStore,
                trieNodeStore);

            var genesisOptions = CreateGenesisOptions();
            await appChain.InitializeAsync(genesisOptions);

            var sequencerConfig = CreateSequencerConfig();
            var sequencer = new Sequencer(appChain, sequencerConfig);
            await sequencer.StartAsync();

            var node = new AppChainNode(appChain, sequencer);
            var rpcClient = new AppChainRpcClient(node, (long)_chainId);

            IWeb3 web3;
            if (_operatorAccount != null)
            {
                web3 = new Web3.Web3(_operatorAccount, rpcClient);
            }
            else
            {
                web3 = new Web3.Web3(rpcClient);
            }
            web3.TransactionManager.UseLegacyAsDefault = true;

            MudGenesisResult? mudResult = null;
            if (_mudConfig?.Enabled == true && _mudConfig.DeployAtGenesis)
            {
                if (string.IsNullOrEmpty(_operatorPrivateKey))
                    throw new InvalidOperationException(
                        "MUD deployment requires operator private key. Use WithOperator(privateKey) instead of WithOperatorAddress().");

                var mudDeployer = new MudWorldDeployer();
                mudResult = await mudDeployer.DeployMudWorldAsync(node, _operatorPrivateKey, _mudConfig.Salt);
            }

            return new AppChainInstance(appChain, sequencer, node, rpcClient, web3, mudResult);
        }

        private (IBlockStore, ITransactionStore, IReceiptStore, ILogStore, IStateStore, ITrieNodeStore?) CreateStores()
        {
            return _storageConfig.Type switch
            {
                StorageType.InMemory => CreateInMemoryStores(),
                StorageType.RocksDb => throw new NotSupportedException("RocksDb storage requires Nethereum.CoreChain.RocksDB package. Use WithStorage(StorageType.InMemory) or inject stores manually."),
                _ => throw new InvalidOperationException($"Unknown storage type: {_storageConfig.Type}")
            };
        }

        private (IBlockStore, ITransactionStore, IReceiptStore, ILogStore, IStateStore, ITrieNodeStore?) CreateInMemoryStores()
        {
            var blockStore = new InMemoryBlockStore();
            var transactionStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var stateStore = new InMemoryStateStore();
            return (blockStore, transactionStore, receiptStore, logStore, stateStore, null);
        }

        private GenesisOptions CreateGenesisOptions()
        {
            var prefunded = new List<string>();

            if (!string.IsNullOrEmpty(_operatorAddress))
                prefunded.Add(_operatorAddress);

            if (_prefundedAddresses != null)
                prefunded.AddRange(_prefundedAddresses);

            return new GenesisOptions
            {
                PrefundedAddresses = prefunded.Count > 0 ? prefunded.ToArray() : null,
                PrefundBalance = _prefundBalance ?? Web3.Web3.Convert.ToWei(1000),
                DeployCreate2Factory = true
            };
        }

        private SequencerConfig CreateSequencerConfig()
        {
            var policyConfig = _trustConfig?.Model switch
            {
                TrustModel.Open => PolicyConfig.OpenAccess,
                TrustModel.Whitelist => _trustConfig.AllowedAddresses != null
                    ? PolicyConfig.RestrictedAccess(new List<string>(_trustConfig.AllowedAddresses))
                    : PolicyConfig.Default,
                _ => PolicyConfig.Default
            };

            return new SequencerConfig
            {
                SequencerAddress = _operatorAddress!,
                SequencerPrivateKey = _operatorPrivateKey,
                BlockTimeMs = _blockTimeMs,
                MaxTransactionsPerBlock = _maxTransactionsPerBlock,
                BlockProductionMode = _blockProductionMode,
                Policy = policyConfig
            };
        }
    }
}
