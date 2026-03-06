using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.AppChain.Genesis;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.AppChain
{
    public class AppChain : IAppChain
    {
        private readonly AppChainConfig _config;
        private readonly IBlockStore _blockStore;
        private readonly ITransactionStore _transactionStore;
        private readonly IReceiptStore _receiptStore;
        private readonly ILogStore _logStore;
        private readonly IStateStore _stateStore;
        private readonly ITrieNodeStore _trieNodeStore;

        private bool _initialized;

        public AppChain(
            AppChainConfig config,
            IBlockStore blockStore,
            ITransactionStore transactionStore,
            IReceiptStore receiptStore,
            ILogStore logStore,
            IStateStore stateStore,
            ITrieNodeStore trieNodeStore = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _blockStore = blockStore ?? throw new ArgumentNullException(nameof(blockStore));
            _transactionStore = transactionStore ?? throw new ArgumentNullException(nameof(transactionStore));
            _receiptStore = receiptStore ?? throw new ArgumentNullException(nameof(receiptStore));
            _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _trieNodeStore = trieNodeStore;
        }

        public AppChainConfig Config => _config;
        public IBlockStore Blocks => _blockStore;
        public IStateStore State => _stateStore;
        public ITransactionStore Transactions => _transactionStore;
        public IReceiptStore Receipts => _receiptStore;
        public ILogStore Logs => _logStore;
        public ITrieNodeStore TrieNodes => _trieNodeStore;
        public string WorldAddress => _config.WorldAddress;
        public string Create2FactoryAddress => Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS;

        public Task InitializeAsync()
        {
            return InitializeAsync(new GenesisOptions());
        }

        public async Task InitializeAsync(GenesisOptions options)
        {
            if (_initialized)
                return;

            var existingHeight = await _blockStore.GetHeightAsync();
            if (existingHeight >= 0)
            {
                await ValidateExistingGenesisAsync();
                _initialized = true;
                return;
            }

            var genesisBuilder = new AppChainGenesisBuilder(_config, _stateStore);

            if (options.PrefundedAddresses != null)
            {
                var balance = options.PrefundBalance ?? _config.InitialBalance;
                foreach (var address in options.PrefundedAddresses)
                {
                    genesisBuilder.AddPrefundedAccount(address, balance);
                }
            }

            if (options.DeployCreate2Factory)
            {
                var create2Builder = new Create2FactoryGenesisBuilder(_stateStore);
                await create2Builder.DeployCreate2FactoryAsync();
            }

            var genesisResult = await genesisBuilder.BuildGenesisBlockAsync();
            await _blockStore.SaveAsync(genesisResult.Header, genesisResult.BlockHash);

            _initialized = true;
        }

        public async Task ApplyGenesisStateAsync(GenesisOptions options)
        {
            var genesisBuilder = new AppChainGenesisBuilder(_config, _stateStore);

            if (options.PrefundedAddresses != null)
            {
                var balance = options.PrefundBalance ?? _config.InitialBalance;
                foreach (var address in options.PrefundedAddresses)
                {
                    genesisBuilder.AddPrefundedAccount(address, balance);
                }
            }

            if (options.DeployCreate2Factory)
            {
                var create2Builder = new Create2FactoryGenesisBuilder(_stateStore);
                await create2Builder.DeployCreate2FactoryAsync();
            }

            await genesisBuilder.ApplyGenesisStateAsync();

            _initialized = true;
        }

        public async Task<BigInteger> GetBlockNumberAsync()
        {
            EnsureInitialized();
            return await _blockStore.GetHeightAsync();
        }

        public async Task<BlockHeader?> GetBlockByNumberAsync(BigInteger blockNumber)
        {
            return await _blockStore.GetByNumberAsync(blockNumber);
        }

        public async Task<BlockHeader?> GetBlockByHashAsync(byte[] blockHash)
        {
            return await _blockStore.GetByHashAsync(blockHash);
        }

        public async Task<BlockHeader?> GetLatestBlockAsync()
        {
            return await _blockStore.GetLatestAsync();
        }

        public async Task<BigInteger> GetBalanceAsync(string address)
        {
            var account = await _stateStore.GetAccountAsync(address);
            return account?.Balance ?? BigInteger.Zero;
        }

        public async Task<BigInteger> GetNonceAsync(string address)
        {
            var account = await _stateStore.GetAccountAsync(address);
            return account?.Nonce ?? BigInteger.Zero;
        }

        public async Task<byte[]?> GetCodeAsync(string address)
        {
            var account = await _stateStore.GetAccountAsync(address);
            if (account?.CodeHash == null)
                return null;

            return await _stateStore.GetCodeAsync(account.CodeHash);
        }

        public async Task<byte[]?> GetStorageAtAsync(string address, BigInteger slot)
        {
            return await _stateStore.GetStorageAsync(address, slot);
        }

        public async Task<Account?> GetAccountAsync(string address)
        {
            return await _stateStore.GetAccountAsync(address);
        }

        public async Task<ISignedTransaction?> GetTransactionByHashAsync(byte[] txHash)
        {
            return await _transactionStore.GetByHashAsync(txHash);
        }

        public async Task<Receipt?> GetTransactionReceiptAsync(byte[] txHash)
        {
            return await _receiptStore.GetByTxHashAsync(txHash);
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
                throw new InvalidOperationException("AppChain must be initialized by calling InitializeAsync() before use.");
        }

        private async Task ValidateExistingGenesisAsync()
        {
            var genesisBlock = await _blockStore.GetByNumberAsync(0);
            if (genesisBlock == null)
            {
                throw new InvalidOperationException("Existing chain has no genesis block");
            }

            if (_config.GenesisHash != null && _config.GenesisHash.Length > 0)
            {
                var storedGenesisHash = await _blockStore.GetHashByNumberAsync(0);
                if (storedGenesisHash == null || !storedGenesisHash.AsSpan().SequenceEqual(_config.GenesisHash))
                {
                    throw new InvalidOperationException(
                        $"Genesis hash mismatch: stored genesis does not match configured genesis. " +
                        $"This may indicate a chain ID or configuration mismatch. " +
                        $"Expected: {(_config.GenesisHash ?? Array.Empty<byte>()).ToHex()}, " +
                        $"Got: {(storedGenesisHash ?? Array.Empty<byte>()).ToHex()}");
                }
            }
        }
    }
}
