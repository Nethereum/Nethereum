using System.Collections.Generic;
using System.Numerics;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class RootCalculatorTests
    {
        private readonly RootCalculator _calculator;

        public RootCalculatorTests()
        {
            _calculator = new RootCalculator();
        }

        [Fact]
        public void EmptyTransactionsRoot_ShouldReturnEmptyTrieHash()
        {
            var root = _calculator.CalculateTransactionsRoot(new List<byte[]>());
            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, root);
        }

        [Fact]
        public void NullTransactionsRoot_ShouldReturnEmptyTrieHash()
        {
            var root = _calculator.CalculateTransactionsRoot(null);
            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, root);
        }

        [Fact]
        public void EmptyReceiptsRoot_ShouldReturnEmptyTrieHash()
        {
            var root = _calculator.CalculateReceiptsRoot(new List<Receipt>());
            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, root);
        }

        [Fact]
        public void NullReceiptsRoot_ShouldReturnEmptyTrieHash()
        {
            var root = _calculator.CalculateReceiptsRoot(null);
            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, root);
        }

        [Fact]
        public void EmptyStateRoot_ShouldReturnEmptyTrieHash()
        {
            var root = _calculator.CalculateStateRoot(new Dictionary<byte[], Account>());
            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, root);
        }

        [Fact]
        public void NullStateRoot_ShouldReturnEmptyTrieHash()
        {
            var root = _calculator.CalculateStateRoot(null);
            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, root);
        }

        [Fact]
        public void SingleTransactionRoot_ShouldBeConsistent()
        {
            var tx1 = "0xf86c098504a817c800825208943535353535353535353535353535353535353535880de0b6b3a76400008025a028ef61340bd939bc2195fe537567866003e1a15d3c71ff63e1590620aa636276a067cbe9d8997f761aecb703304b3800ccf555c9f3dc64214b297fb1966a3b6d83".HexToByteArray();

            var root1 = _calculator.CalculateTransactionsRoot(new List<byte[]> { tx1 });
            var root2 = _calculator.CalculateTransactionsRoot(new List<byte[]> { tx1 });

            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH, root1);
            Assert.Equal(root1, root2);
        }

        [Fact]
        public void MultipleTransactionsRoot_OrderMatters()
        {
            var tx1 = "0xf86c098504a817c800825208943535353535353535353535353535353535353535880de0b6b3a76400008025a028ef61340bd939bc2195fe537567866003e1a15d3c71ff63e1590620aa636276a067cbe9d8997f761aecb703304b3800ccf555c9f3dc64214b297fb1966a3b6d83".HexToByteArray();
            var tx2 = "0xf86c0a8504a817c800825208943535353535353535353535353535353535353535880de0b6b3a76400008025a028ef61340bd939bc2195fe537567866003e1a15d3c71ff63e1590620aa636276a067cbe9d8997f761aecb703304b3800ccf555c9f3dc64214b297fb1966a3b6d83".HexToByteArray();

            var rootAB = _calculator.CalculateTransactionsRoot(new List<byte[]> { tx1, tx2 });
            var rootBA = _calculator.CalculateTransactionsRoot(new List<byte[]> { tx2, tx1 });

            Assert.NotEqual(rootAB, rootBA);
        }

        [Fact]
        public void SingleReceiptRoot_ShouldBeConsistent()
        {
            var receipt = Receipt.CreateStatusReceipt(
                success: true,
                cumulativeGasUsed: 21000,
                bloom: new byte[256],
                logs: new List<Log>()
            );

            var root1 = _calculator.CalculateReceiptsRoot(new List<Receipt> { receipt });
            var root2 = _calculator.CalculateReceiptsRoot(new List<Receipt> { receipt });

            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH, root1);
            Assert.Equal(root1, root2);
        }

        [Fact]
        public void SingleAccountStateRoot_ShouldBeConsistent()
        {
            var hasher = new Sha3Keccack();
            var addressHash = hasher.CalculateHash("0x1234567890123456789012345678901234567890".HexToByteArray());

            var accounts = new Dictionary<byte[], Account>(new ByteArrayComparer())
            {
                { addressHash, new Account { Balance = 1000000, Nonce = 1 } }
            };

            var root1 = _calculator.CalculateStateRoot(accounts);
            var root2 = _calculator.CalculateStateRoot(accounts);

            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH, root1);
            Assert.Equal(root1, root2);
        }

        [Fact]
        public void DifferentAccountBalances_ShouldProduceDifferentRoots()
        {
            var hasher = new Sha3Keccack();
            var addressHash = hasher.CalculateHash("0x1234567890123456789012345678901234567890".HexToByteArray());

            var accounts1 = new Dictionary<byte[], Account>(new ByteArrayComparer())
            {
                { addressHash, new Account { Balance = 1000000, Nonce = 1 } }
            };

            var accounts2 = new Dictionary<byte[], Account>(new ByteArrayComparer())
            {
                { addressHash, new Account { Balance = 2000000, Nonce = 1 } }
            };

            var root1 = _calculator.CalculateStateRoot(accounts1);
            var root2 = _calculator.CalculateStateRoot(accounts2);

            Assert.NotEqual(root1, root2);
        }

        [Fact]
        public void StorageRoot_ShouldBeConsistent()
        {
            var hasher = new Sha3Keccack();
            var keyHash = hasher.CalculateHash(BigInteger.Zero.ToBytesForRLPEncoding());

            var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer())
            {
                { keyHash, BigInteger.One.ToBytesForRLPEncoding() }
            };

            var root1 = _calculator.CalculateStorageRoot(storage);
            var root2 = _calculator.CalculateStorageRoot(storage);

            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH, root1);
            Assert.Equal(root1, root2);
        }

        [Fact]
        public void BlockRoots_ShouldCalculateAllThreeRoots()
        {
            var hasher = new Sha3Keccack();
            var addressHash = hasher.CalculateHash("0x1234567890123456789012345678901234567890".HexToByteArray());

            var accounts = new Dictionary<byte[], Account>(new ByteArrayComparer())
            {
                { addressHash, new Account { Balance = 1000000, Nonce = 1 } }
            };

            var tx = "0xf86c098504a817c800825208943535353535353535353535353535353535353535880de0b6b3a76400008025a028ef61340bd939bc2195fe537567866003e1a15d3c71ff63e1590620aa636276a067cbe9d8997f761aecb703304b3800ccf555c9f3dc64214b297fb1966a3b6d83".HexToByteArray();
            var transactions = new List<byte[]> { tx };

            var receipt = Receipt.CreateStatusReceipt(true, 21000, new byte[256], new List<Log>());
            var receipts = new List<Receipt> { receipt };

            var blockRoots = _calculator.CalculateBlockRoots(accounts, transactions, receipts);

            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH, blockRoots.StateRoot);
            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH, blockRoots.TransactionsRoot);
            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH, blockRoots.ReceiptsRoot);
        }

        [Fact]
        public void RootCalculation_WithNodeStore_ShouldProduceSameRootAsWithoutStore()
        {
            var nodeStore = new InMemoryTrieNodeStore();
            var tx = "0xf86c098504a817c800825208943535353535353535353535353535353535353535880de0b6b3a76400008025a028ef61340bd939bc2195fe537567866003e1a15d3c71ff63e1590620aa636276a067cbe9d8997f761aecb703304b3800ccf555c9f3dc64214b297fb1966a3b6d83".HexToByteArray();

            var rootWithStore = _calculator.CalculateTransactionsRoot(new List<byte[]> { tx }, nodeStore);
            var rootWithoutStore = _calculator.CalculateTransactionsRoot(new List<byte[]> { tx });

            Assert.Equal(rootWithStore, rootWithoutStore);
        }

        [Fact]
        public void EmptyBlockRoots_StaticProperty_ShouldReturnEmptyTrieHashes()
        {
            var empty = BlockRoots.Empty;

            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, empty.StateRoot);
            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, empty.TransactionsRoot);
            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, empty.ReceiptsRoot);
        }
    }
}
