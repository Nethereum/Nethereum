using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Chain.TestData;
using AppChainCore = Nethereum.AppChain.AppChain;
using SequencerBlockProducer = Nethereum.AppChain.Sequencer.BlockProducer;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

using Nethereum.Chain.TestData;

namespace Nethereum.Chain.TestData.UnitTests
{
    public sealed class InProcessSequencerDriver : IVectorChainDriver
    {
        public const string SequencerPrivateKey = "0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";

        private readonly BigInteger _chainId;
        private readonly Nethereum.AppChain.AppChainConfig _config;
        private readonly SequencerBlockProducer _producer;

        private const long GenesisTimestamp = 1_700_000_000;
        private const long BlockIntervalSeconds = 12;
        private readonly Dictionary<string, int> _nonces = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ISignedTransaction> _pending = new List<ISignedTransaction>();
        private readonly List<ProducedBlock> _produced = new List<ProducedBlock>();
        private readonly List<string> _deployed = new List<string>();
        private readonly List<(BlockHeader Header, IList<ISignedTransaction> Transactions)> _blockData
            = new List<(BlockHeader, IList<ISignedTransaction>)>();

        public const string ChainName = "workload";

        public WorkloadAccounts Accounts { get; }
        public IBlockStore Blocks { get; }
        public IStateStore State { get; }
        public ITransactionStore Transactions { get; }
        public IReceiptStore Receipts { get; }
        public ILogStore Logs { get; }
        public IWithdrawalStore Withdrawals { get; }
        public ITrieNodeStore TrieNodes { get; }
        public IBlockAccessListStore BlockAccessLists { get; }
        public string SequencerAddress { get; }
        public string Hardfork { get; }
        public BigInteger ChainId => _chainId;
        public Nethereum.AppChain.GenesisOptions Genesis { get; }
        public IReadOnlyList<ProducedBlock> Produced => _produced;
        public IReadOnlyList<(BlockHeader Header, IList<ISignedTransaction> Transactions)> ProducedBlockData => _blockData;
        public ChainStores Stores => new ChainStores(Blocks, State, Transactions, Receipts, Logs, BlockAccessLists);

        private readonly SequencerBlockProductionStrategy _blockStrategy;

        private InProcessSequencerDriver(
            BigInteger chainId, Nethereum.AppChain.AppChainConfig config, string sequencerAddress, Nethereum.AppChain.GenesisOptions genesis,
            WorkloadAccounts accounts, SequencerBlockProducer producer,
            IBlockStore blocks, IStateStore state, ITransactionStore txs,
            IReceiptStore receipts, ILogStore logs, IWithdrawalStore withdrawals, ITrieNodeStore trie,
            IBlockAccessListStore blockAccessLists, string hardfork, SequencerBlockProductionStrategy blockStrategy)
        {
            _chainId = chainId;
            _config = config;
            SequencerAddress = sequencerAddress;
            Genesis = genesis;
            Accounts = accounts;
            _producer = producer;
            Blocks = blocks; State = state; Transactions = txs; Receipts = receipts; Logs = logs; Withdrawals = withdrawals; TrieNodes = trie;
            BlockAccessLists = blockAccessLists;
            Hardfork = hardfork;
            _blockStrategy = blockStrategy;
        }

        public static async Task<InProcessSequencerDriver> CreateAsync(BigInteger? chainId = null, int generatedAccounts = 0,
            IEnumerable<string> extraPrefunded = null, string hardfork = "prague")
        {
            var cid = chainId ?? new BigInteger(420420);
            var accounts = new WorkloadAccounts(generatedAccounts);
            var sequencerAddress = new EthECKey(SequencerPrivateKey).GetPublicAddress();

            var blocks = new InMemoryBlockStore();
            var txs = new InMemoryTransactionStore(blocks);
            var receipts = new InMemoryReceiptStore();
            var logs = new InMemoryLogStore();
            var withdrawals = new InMemoryWithdrawalStore(blocks);
            var state = new InMemoryStateStore();
            var trie = new InMemoryContentNodeStore();
            var blockAccessLists = new InMemoryBlockAccessListStore(blocks);

            var config = Nethereum.AppChain.AppChainConfig.CreateWithName(ChainName, cid);
            config.SequencerAddress = sequencerAddress;
            config.Hardfork = hardfork;

            var appChain = new AppChainCore(config, blocks, txs, receipts, logs, state, trie);

            var prefunded = ChainGenesisData
                .PrefundedRoster(generatedAccounts, sequencerAddress)
                .Concat(extraPrefunded ?? Enumerable.Empty<string>())
                .ToArray();
            var genesis = new Nethereum.AppChain.GenesisOptions
            {
                DeployCreate2Factory = true,
                PrefundedAddresses = prefunded,
                PrefundBalance = ChainGenesisData.RosterPrefundBalance
            };
            await appChain.InitializeAsync(genesis);

            var chainConfig = new ChainConfig { ChainId = cid, BaseFee = BigInteger.Zero, Coinbase = sequencerAddress, Hardfork = hardfork };
            var txProcessor = new TransactionProcessor(state, blocks, chainConfig, new TransactionVerificationAndRecoveryImp());
            var blockStrategy = new SequencerBlockProductionStrategy(config);
            var producer = new SequencerBlockProducer(
                appChain, txProcessor, strategy: blockStrategy, withdrawalStore: withdrawals, blockAccessListStore: blockAccessLists);

            return new InProcessSequencerDriver(cid, config, sequencerAddress, genesis, accounts, producer, blocks, state, txs, receipts, logs, withdrawals, trie, blockAccessLists, hardfork, blockStrategy);
        }

        private sealed class SequencerBlockProductionStrategy : Nethereum.CoreChain.Consensus.IBlockProductionStrategy
        {
            private readonly Nethereum.AppChain.AppChainConfig _config;
            public List<Withdrawal> PendingWithdrawals { get; set; }

            public SequencerBlockProductionStrategy(Nethereum.AppChain.AppChainConfig config) => _config = config;

            public bool CanProduceBlock(long blockNumber) => true;

            public Task<TimeSpan> GetSigningDelayAsync(long blockNumber, System.Threading.CancellationToken cancellationToken = default)
                => Task.FromResult(TimeSpan.Zero);

            public BlockProductionOptions PrepareBlockOptions(long blockNumber, BlockHeader parentHeader)
                => new BlockProductionOptions
                {
                    Timestamp = GenesisTimestamp + BlockIntervalSeconds * blockNumber,
                    Coinbase = _config.Coinbase,
                    BaseFee = _config.BaseFee,
                    BlockGasLimit = _config.BlockGasLimit,
                    ChainId = _config.ChainId,
                    Difficulty = BigInteger.Zero,
                    ExtraData = System.Text.Encoding.UTF8.GetBytes($"AppChain:{_config.AppChainName}"),
                    Withdrawals = PendingWithdrawals,
                };

            public Task FinalizeBlockAsync(BlockHeader header, byte[] blockHash, BlockProductionResult result)
                => Task.CompletedTask;
        }

        public void QueueTransfer(RosterAccount from, string to, BigInteger valueWei)
        {
            var nonce = NextNonce(from.Address);
            var tx = new Transaction1559(
                chainId: _chainId,
                nonce: nonce,
                maxPriorityFeePerGas: BigInteger.Zero,
                maxFeePerGas: new BigInteger(1_000_000_000),
                gasLimit: new BigInteger(21000),
                receiverAddress: to,
                amount: valueWei,
                data: null,
                accessList: null);
            var sig = from.Key.SignAndCalculateYParityV(tx.RawHash);
            tx.SetSignature(new Signature { R = sig.R, S = sig.S, V = sig.V });
            _pending.Add(tx);
        }

        public string QueueDeploy(RosterAccount from, byte[] bytecode)
            => QueueDeploy(from, bytecode, new BigInteger(1_000_000));

        public string QueueDeploy(RosterAccount from, byte[] bytecode, BigInteger gasLimit)
        {
            var nonce = NextNonce(from.Address);
            var tx = new Transaction1559(
                chainId: _chainId,
                nonce: nonce,
                maxPriorityFeePerGas: BigInteger.Zero,
                maxFeePerGas: new BigInteger(1_000_000_000),
                gasLimit: gasLimit,
                receiverAddress: null,
                amount: BigInteger.Zero,
                data: bytecode.ToHex(true),
                accessList: null);
            var sig = from.Key.SignAndCalculateYParityV(tx.RawHash);
            tx.SetSignature(new Signature { R = sig.R, S = sig.S, V = sig.V });
            _pending.Add(tx);
            var address = Nethereum.Util.ContractUtils.CalculateContractAddress(from.Address, nonce);
            _deployed.Add(address);
            return address;
        }

        public IReadOnlyList<string> DeployedContracts => _deployed;

        public void QueueCall(RosterAccount from, string to, byte[] data)
            => QueueCall(from, to, data, new BigInteger(200_000));

        public void QueueCall(RosterAccount from, string to, byte[] data, BigInteger gasLimit)
        {
            var nonce = NextNonce(from.Address);
            var tx = new Transaction1559(
                chainId: _chainId,
                nonce: nonce,
                maxPriorityFeePerGas: BigInteger.Zero,
                maxFeePerGas: new BigInteger(1_000_000_000),
                gasLimit: gasLimit,
                receiverAddress: to,
                amount: BigInteger.Zero,
                data: (data == null || data.Length == 0) ? null : data.ToHex(true),
                accessList: null);
            var sig = from.Key.SignAndCalculateYParityV(tx.RawHash);
            tx.SetSignature(new Signature { R = sig.R, S = sig.S, V = sig.V });
            _pending.Add(tx);
        }

        private readonly List<Withdrawal> _pendingWithdrawals = new List<Withdrawal>();
        private ulong _nextWithdrawalIndex;

        /// <summary>Queue an EIP-4895 withdrawal credited to <paramref name="toAddress"/> in the next block.
        /// Configurable core behaviour the appchain doesn't use by default, exercised here for snap coverage.</summary>
        public void QueueWithdrawal(string toAddress, ulong amountInGwei)
        {
            _pendingWithdrawals.Add(new Withdrawal
            {
                Index = _nextWithdrawalIndex++,
                ValidatorIndex = 0,
                Address = toAddress.HexToByteArray(),
                AmountInGwei = amountInGwei
            });
        }

        public async Task<ProducedBlock> ProduceBlockAsync()
        {
            var toProduce = _pending.ToList();
            _pending.Clear();
            var withdrawals = _pendingWithdrawals.Count > 0 ? _pendingWithdrawals.ToList() : null;
            _pendingWithdrawals.Clear();

            _blockStrategy.PendingWithdrawals = withdrawals;
            var result = await _producer.ProduceBlockAsync(toProduce);

            var logCount = result.TransactionResults?.Sum(r => r.Receipt?.Logs?.Count ?? 0) ?? 0;
            var pb = new ProducedBlock(
                (long)result.Header.BlockNumber,
                result.BlockHash,
                result.Header.StateRoot,
                result.Header.TransactionsHash,
                result.Header.ReceiptHash,
                result.TransactionResults?.Count ?? 0,
                logCount);
            _produced.Add(pb);
            _blockData.Add((result.Header, toProduce));
            return pb;
        }

        private int NextNonce(string address)
        {
            _nonces.TryGetValue(address, out var n);
            _nonces[address] = n + 1;
            return n;
        }
    }
}
