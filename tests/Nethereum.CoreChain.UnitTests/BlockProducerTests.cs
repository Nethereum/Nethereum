using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockProducerTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string RecipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private static readonly BigInteger ChainId = 1337;
        private static readonly LegacyTransactionSigner Signer = new();

        private readonly InMemoryBlockStore _blockStore;
        private readonly InMemoryTransactionStore _transactionStore;
        private readonly InMemoryReceiptStore _receiptStore;
        private readonly InMemoryLogStore _logStore;
        private readonly InMemoryStateStore _stateStore;
        private readonly TransactionProcessor _transactionProcessor;
        private readonly BlockProducer _blockProducer;

        public BlockProducerTests()
        {
            _blockStore = new InMemoryBlockStore();
            _transactionStore = new InMemoryTransactionStore(_blockStore);
            _receiptStore = new InMemoryReceiptStore();
            _logStore = new InMemoryLogStore();
            _stateStore = new InMemoryStateStore();

            var config = new ChainConfig { ChainId = ChainId, BlockGasLimit = 30_000_000, BaseFee = 0 };
            var txVerifier = new TransactionVerificationAndRecoveryImp();
            _transactionProcessor = new TransactionProcessor(_stateStore, _blockStore, config, txVerifier);

            _blockProducer = new BlockProducer(
                _blockStore, _transactionStore, _receiptStore, _logStore, _stateStore,
                _transactionProcessor);
        }

        private BlockProductionOptions DefaultOptions() => new BlockProductionOptions
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Coinbase = SenderAddress,
            BaseFee = 0,
            BlockGasLimit = 30_000_000,
            Difficulty = 1,
            ChainId = ChainId
        };

        private async Task FundSender(BigInteger amount, BigInteger nonce = default)
        {
            await _stateStore.SaveAccountAsync(SenderAddress, new Account
            {
                Balance = amount,
                Nonce = nonce
            });
        }

        private ISignedTransaction CreateSignedTransaction(string to, BigInteger value, BigInteger nonce, BigInteger gasPrice = default, BigInteger gasLimit = default)
        {
            if (gasPrice == 0) gasPrice = 1;
            if (gasLimit == 0) gasLimit = 21_000;
            var signedTxHex = Signer.SignTransaction(
                PrivateKey.HexToByteArray(),
                ChainId,
                to,
                value,
                nonce,
                gasPrice,
                gasLimit,
                "");
            return TransactionFactory.CreateTransaction(signedTxHex);
        }

        [Fact]
        public async Task ProduceBlock_EmptyTransactions_CreatesBlock()
        {
            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction>(), DefaultOptions());

            Assert.NotNull(result);
            Assert.NotNull(result.BlockHash);
            Assert.Equal(1, result.Header.BlockNumber);
            Assert.Equal(0, result.TransactionResults.Count);
            Assert.Equal(0, result.SuccessfulTransactions);
            Assert.Equal(0, result.FailedTransactions);
        }

        [Fact]
        public async Task ProduceBlock_BlockNumberIncrements()
        {
            var options = DefaultOptions();

            var result1 = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction>(), options);
            Assert.Equal(1, result1.Header.BlockNumber);

            options.Timestamp++;
            var result2 = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction>(), options);
            Assert.Equal(2, result2.Header.BlockNumber);

            options.Timestamp++;
            var result3 = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction>(), options);
            Assert.Equal(3, result3.Header.BlockNumber);
        }

        [Fact]
        public async Task ProduceBlock_ParentHashLinksCorrectly()
        {
            var options = DefaultOptions();

            var result1 = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction>(), options);

            options.Timestamp++;
            var result2 = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction>(), options);

            Assert.Equal(result1.BlockHash, result2.Header.ParentHash);
        }

        [Fact]
        public async Task ProduceBlock_BlockHashIsNotNull()
        {
            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction>(), DefaultOptions());

            Assert.NotNull(result.BlockHash);
            Assert.True(result.BlockHash.Length > 0);
        }

        [Fact]
        public async Task ProduceBlock_SavesBlockToStore()
        {
            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction>(), DefaultOptions());

            var storedBlock = await _blockStore.GetByHashAsync(result.BlockHash);
            Assert.NotNull(storedBlock);
            Assert.Equal(result.Header.BlockNumber, storedBlock.BlockNumber);
        }

        [Fact]
        public async Task ProduceBlock_WithTransaction_IncludesTransaction()
        {
            await FundSender(BigInteger.Parse("10000000000000000000"));

            var tx = CreateSignedTransaction(RecipientAddress, 1000, 0);
            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction> { tx }, DefaultOptions());

            Assert.Equal(1, result.TransactionResults.Count);
            Assert.True(result.SuccessfulTransactions > 0 || result.FailedTransactions > 0);
        }

        [Fact]
        public async Task ProduceBlock_WithValidTransfer_Succeeds()
        {
            await FundSender(BigInteger.Parse("10000000000000000000"));

            var tx = CreateSignedTransaction(RecipientAddress, 1000, 0, gasPrice: 1, gasLimit: 21_000);
            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction> { tx }, DefaultOptions());

            Assert.Equal(1, result.SuccessfulTransactions);
            Assert.Equal(0, result.FailedTransactions);
            Assert.True(result.Header.GasUsed > 0);
        }

        [Fact]
        public async Task ProduceBlock_TransactionSavedToStore()
        {
            await FundSender(BigInteger.Parse("10000000000000000000"));

            var tx = CreateSignedTransaction(RecipientAddress, 1000, 0);
            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction> { tx }, DefaultOptions());

            var storedTx = await _transactionStore.GetByHashAsync(tx.Hash);
            Assert.NotNull(storedTx);
        }

        [Fact]
        public async Task ProduceBlock_ReceiptSavedToStore()
        {
            await FundSender(BigInteger.Parse("10000000000000000000"));

            var tx = CreateSignedTransaction(RecipientAddress, 1000, 0);
            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction> { tx }, DefaultOptions());

            var receipt = await _receiptStore.GetByTxHashAsync(tx.Hash);
            Assert.NotNull(receipt);
        }

        [Fact]
        public async Task ProduceBlock_MultipleTransactions_OrdersByNonce()
        {
            await FundSender(BigInteger.Parse("10000000000000000000"));

            var tx0 = CreateSignedTransaction(RecipientAddress, 100, 0);
            var tx1 = CreateSignedTransaction(RecipientAddress, 200, 1);

            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction> { tx1, tx0 }, DefaultOptions());

            Assert.Equal(2, result.TransactionResults.Count);
            Assert.Equal(2, result.SuccessfulTransactions);
        }

        [Fact]
        public async Task ProduceBlock_GasLimitExceeded_SkipsTransaction()
        {
            await FundSender(BigInteger.Parse("10000000000000000000"));

            var tx = CreateSignedTransaction(RecipientAddress, 100, 0, gasLimit: 31_000_000);
            var options = DefaultOptions();
            options.BlockGasLimit = 30_000_000;

            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction> { tx }, options);

            Assert.Equal(0, result.TransactionResults.Count);
        }

        [Fact]
        public async Task ProduceBlock_HeaderHasCorrectCoinbase()
        {
            var options = DefaultOptions();
            options.Coinbase = "0x1234567890abcdef1234567890abcdef12345678";

            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction>(), options);

            Assert.Equal(options.Coinbase, result.Header.Coinbase);
        }

        [Fact]
        public async Task ProduceBlock_HeaderHasCorrectTimestamp()
        {
            var options = DefaultOptions();
            options.Timestamp = 1700000000;

            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction>(), options);

            Assert.Equal(1700000000, result.Header.Timestamp);
        }

        [Fact]
        public async Task ProduceBlock_HeaderHasCorrectGasLimit()
        {
            var options = DefaultOptions();
            options.BlockGasLimit = 15_000_000;

            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction>(), options);

            Assert.Equal(15_000_000, result.Header.GasLimit);
        }

        [Fact]
        public async Task ProduceBlock_EmptyBlock_HasZeroGasUsed()
        {
            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction>(), DefaultOptions());

            Assert.Equal(0, result.Header.GasUsed);
        }

        [Fact]
        public async Task ProduceBlock_FirstBlock_ParentHashIsZero()
        {
            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction>(), DefaultOptions());

            Assert.Equal(new byte[32], result.Header.ParentHash);
        }

        [Fact]
        public async Task ProduceBlock_EmptyBlock_HasEmptyTrieRoot()
        {
            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction>(), DefaultOptions());

            Assert.NotNull(result.Header.TransactionsHash);
            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, result.Header.TransactionsHash);
        }

        [Fact]
        public async Task ProduceBlock_WithTransaction_HasNonEmptyTransactionsRoot()
        {
            await FundSender(BigInteger.Parse("10000000000000000000"));

            var tx = CreateSignedTransaction(RecipientAddress, 100, 0);
            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction> { tx }, DefaultOptions());

            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH, result.Header.TransactionsHash);
        }

        [Fact]
        public async Task ProduceBlock_InvalidNonce_SkipsTransaction()
        {
            await FundSender(BigInteger.Parse("10000000000000000000"));

            var tx = CreateSignedTransaction(RecipientAddress, 100, 5);
            var result = await _blockProducer.ProduceBlockAsync(
                new List<ISignedTransaction> { tx }, DefaultOptions());

            Assert.Equal(0, result.TransactionResults.Count);
        }

        [Fact]
        public async Task ProduceBlock_ConcurrentCalls_AreSerializedByLock()
        {
            var tasks = new List<Task<BlockProductionResult>>();
            for (int i = 0; i < 5; i++)
            {
                var opts = DefaultOptions();
                opts.Timestamp += i;
                tasks.Add(_blockProducer.ProduceBlockAsync(
                    new List<ISignedTransaction>(), opts));
            }

            var results = await Task.WhenAll(tasks);

            var blockNumbers = new HashSet<BigInteger>();
            foreach (var r in results)
            {
                blockNumbers.Add(r.Header.BlockNumber);
            }

            Assert.Equal(5, blockNumbers.Count);
        }

        [Fact]
        public async Task ProduceBlock_NullTransactionsList_Throws()
        {
            await Assert.ThrowsAsync<NullReferenceException>(() =>
                _blockProducer.ProduceBlockAsync(null, DefaultOptions()));
        }

        [Fact]
        public void Constructor_NullBlockStore_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BlockProducer(null, _transactionStore, _receiptStore, _logStore, _stateStore, _transactionProcessor));
        }

        [Fact]
        public void Constructor_NullTransactionStore_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BlockProducer(_blockStore, null, _receiptStore, _logStore, _stateStore, _transactionProcessor));
        }

        [Fact]
        public void Constructor_NullReceiptStore_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BlockProducer(_blockStore, _transactionStore, null, _logStore, _stateStore, _transactionProcessor));
        }

        [Fact]
        public void Constructor_NullLogStore_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BlockProducer(_blockStore, _transactionStore, _receiptStore, null, _stateStore, _transactionProcessor));
        }

        [Fact]
        public void Constructor_NullStateStore_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BlockProducer(_blockStore, _transactionStore, _receiptStore, _logStore, null, _transactionProcessor));
        }

        [Fact]
        public void Constructor_NullTransactionProcessor_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BlockProducer(_blockStore, _transactionStore, _receiptStore, _logStore, _stateStore, null));
        }
    }
}
