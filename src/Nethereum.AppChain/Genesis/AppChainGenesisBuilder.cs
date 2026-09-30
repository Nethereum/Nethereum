using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.AppChain.Genesis
{
    public class GenesisBlockResult
    {
        public required BlockHeader Header { get; set; }
        public required byte[] BlockHash { get; set; }
    }

    public class AppChainGenesisBuilder
    {
        private readonly AppChainConfig _config;
        private readonly IStateStore _stateStore;
        private readonly Dictionary<string, BigInteger> _prefundedAccounts = new Dictionary<string, BigInteger>();
        private readonly Sha3Keccack _keccak = new Sha3Keccack();
        private readonly IBlockHashProvider _blockHashProvider;
        private readonly ITrieNodeStore _trieNodeStore;

        public AppChainGenesisBuilder(
            AppChainConfig config, IStateStore stateStore, IBlockHashProvider blockHashProvider = null,
            ITrieNodeStore trieNodeStore = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _blockHashProvider = blockHashProvider ?? RlpKeccakBlockHashProvider.Instance;
            _trieNodeStore = trieNodeStore;
        }

        public Task<byte[]> PersistStateTrieAsync() =>
            new IncrementalStateRootCalculator(_stateStore, _trieNodeStore).ComputeStateRootAsync();

        public void AddPrefundedAccount(string address, BigInteger balance)
        {
            var normalized = AddressUtil.Current.ConvertToValid20ByteAddress(address);
            _prefundedAccounts[normalized] = balance;
        }

        public async Task ApplyGenesisStateAsync()
        {
            // Same allocation DevChain's genesis makes, from the same
            // definition — an AppChain pinned at Prague needs the EIP-2935
            // history contract in state for BLOCKHASH to resolve, exactly as
            // mainnet does.
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(_stateStore, _config.NewestForkThisChainRuns);

            foreach (var kvp in _prefundedAccounts)
            {
                var account = new Account { Balance = kvp.Value };

                var existing = await _stateStore.GetAccountAsync(kvp.Key).ConfigureAwait(false);
                if (existing != null)
                {
                    account.Nonce = existing.Nonce;
                    account.CodeHash = existing.CodeHash;
                }

                await _stateStore.SaveAccountAsync(kvp.Key, account).ConfigureAwait(false);
            }
        }

        public async Task<GenesisBlockResult> BuildGenesisBlockAsync()
        {
            await ApplyGenesisStateAsync();

            var stateRoot = await PersistStateTrieAsync();

            var emptyListHash = _keccak.CalculateHash(RLP.RLP.EncodeList());

            var genesisBlock = new BlockHeader
            {
                BlockNumber = 0,
                ParentHash = new byte[32],
                UnclesHash = emptyListHash,
                StateRoot = stateRoot,
                TransactionsHash = DefaultValues.EMPTY_TRIE_HASH,
                ReceiptHash = DefaultValues.EMPTY_TRIE_HASH,
                LogsBloom = new byte[256],
                Timestamp = _config.GenesisTimestamp,
                GasLimit = (long)_config.BlockGasLimit,
                GasUsed = 0,
                Coinbase = _config.Coinbase,
                Difficulty = 0,
                MixHash = new byte[32],
                Nonce = new byte[8],
                ExtraData = System.Text.Encoding.UTF8.GetBytes($"AppChain:{_config.AppChainName}"),
                BaseFee = _config.BaseFee
            };

            GenesisHeaderFields.Apply(genesisBlock, _config.PinnedFork);

            var blockHash = BlockHashCalculator.ForFork(genesisBlock, _config.PinnedFork, _blockHashProvider);

            return new GenesisBlockResult
            {
                Header = genesisBlock,
                BlockHash = blockHash
            };
        }
    }
}
