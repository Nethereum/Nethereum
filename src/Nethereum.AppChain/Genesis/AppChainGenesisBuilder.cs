using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
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
        private readonly StateRootCalculator _stateRootCalculator = new StateRootCalculator();
        private readonly IBlockHashProvider _blockHashProvider;

        public AppChainGenesisBuilder(AppChainConfig config, IStateStore stateStore, IBlockHashProvider blockHashProvider = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _blockHashProvider = blockHashProvider ?? RlpKeccakBlockHashProvider.Instance;
        }

        public void AddPrefundedAccount(string address, BigInteger balance)
        {
            var normalized = AddressUtil.Current.ConvertToValid20ByteAddress(address);
            _prefundedAccounts[normalized] = balance;
        }

        public async Task ApplyGenesisStateAsync()
        {
            foreach (var kvp in _prefundedAccounts)
            {
                var account = new Account
                {
                    Balance = kvp.Value,
                    Nonce = 0
                };
                await _stateStore.SaveAccountAsync(kvp.Key, account);
            }
        }

        public async Task<GenesisBlockResult> BuildGenesisBlockAsync()
        {
            await ApplyGenesisStateAsync();

            var stateRoot = await _stateRootCalculator.ComputeStateRootAsync(_stateStore);

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
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                GasLimit = (long)_config.BlockGasLimit,
                GasUsed = 0,
                Coinbase = _config.Coinbase,
                Difficulty = 0,
                MixHash = new byte[32],
                Nonce = new byte[8],
                ExtraData = System.Text.Encoding.UTF8.GetBytes($"AppChain:{_config.AppChainName}"),
                BaseFee = _config.BaseFee
            };

            var blockHash = ComputeBlockHash(genesisBlock);

            return new GenesisBlockResult
            {
                Header = genesisBlock,
                BlockHash = blockHash
            };
        }

        private byte[] ComputeBlockHash(BlockHeader header)
            => _blockHashProvider.ComputeBlockHash(header);
    }
}
