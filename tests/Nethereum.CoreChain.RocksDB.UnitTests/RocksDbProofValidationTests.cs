using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbProofValidationTests : IAsyncLifetime
    {
        private RocksDbTestFixture _fixture = null!;
        private DevChainNode _node = null!;
        private readonly string _privateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private readonly string _address = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private readonly string _recipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private readonly BigInteger _chainId = 31337;
        private readonly LegacyTransactionSigner _signer = new();

        public async Task InitializeAsync()
        {
            _fixture = new RocksDbTestFixture();
            var config = new DevChainConfig
            {
                ChainId = _chainId,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            };

            _node = new DevChainNode(
                config,
                _fixture.BlockStore,
                _fixture.TransactionStore,
                _fixture.ReceiptStore,
                _fixture.LogStore,
                _fixture.StateStore,
                _fixture.FilterStore,
                _fixture.TrieNodeStore);

            await _node.StartAsync(new[] { _address }, BigInteger.Parse("10000000000000000000000"));
        }

        public Task DisposeAsync()
        {
            _fixture.Dispose();
            return Task.CompletedTask;
        }

        private ISignedTransaction CreateSignedTransaction(string to, BigInteger value)
        {
            var nonce = _node.GetNonceAsync(_address).Result;
            var signedTxHex = _signer.SignTransaction(
                _privateKey.HexToByteArray(),
                _chainId,
                to,
                value,
                nonce,
                1_000_000_000,
                21_000,
                "");
            return TransactionFactory.CreateTransaction(signedTxHex);
        }

        [Fact]
        public async Task TrieNodes_ArePersistedAfterBlockProduction()
        {
            var tx = CreateSignedTransaction(_recipientAddress, BigInteger.Parse("100000000000000000"));
            var result = await _node.SendTransactionAsync(tx);
            Assert.True(result.Success);

            var block = await _node.GetLatestBlockAsync();
            Assert.NotNull(block);
            Assert.NotNull(block.StateRoot);

            var stateRootNode = _fixture.TrieNodeStore.Get(block.StateRoot);
            Assert.NotNull(stateRootNode);
        }

        [Fact]
        public async Task TransactionsRoot_CanBeVerifiedFromPersistedNodes()
        {
            var tx = CreateSignedTransaction(_recipientAddress, BigInteger.Parse("100000000000000000"));
            var encodedTx = tx.GetRLPEncoded();

            var result = await _node.SendTransactionAsync(tx);
            Assert.True(result.Success);

            var block = await _node.GetLatestBlockAsync();
            Assert.NotNull(block);
            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH, block.TransactionsHash);

            var rootCalculator = new RootCalculator();
            var recomputedRoot = rootCalculator.CalculateTransactionsRoot(new List<byte[]> { encodedTx });

            Assert.True(block.TransactionsHash.SequenceEqual(recomputedRoot),
                "Transaction root from block should match recomputed root");
        }

        [Fact]
        public async Task ReceiptsRoot_CanBeVerifiedFromPersistedNodes()
        {
            var tx = CreateSignedTransaction(_recipientAddress, BigInteger.Parse("100000000000000000"));
            var result = await _node.SendTransactionAsync(tx);
            Assert.True(result.Success);

            var receipt = await _node.GetTransactionReceiptAsync(tx.Hash);
            Assert.NotNull(receipt);

            var block = await _node.GetLatestBlockAsync();
            Assert.NotNull(block);
            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH, block.ReceiptHash);

            var rootCalculator = new RootCalculator();
            var recomputedRoot = rootCalculator.CalculateReceiptsRoot(new List<Receipt> { receipt });

            Assert.True(block.ReceiptHash.SequenceEqual(recomputedRoot),
                "Receipt root from block should match recomputed root");
        }

        [Fact]
        public async Task TransactionProof_CanBeGeneratedAndVerified()
        {
            var tx = CreateSignedTransaction(_recipientAddress, BigInteger.Parse("100000000000000000"));
            var encodedTx = tx.GetRLPEncoded();

            var result = await _node.SendTransactionAsync(tx);
            Assert.True(result.Success);

            var block = await _node.GetLatestBlockAsync();
            Assert.NotNull(block);

            var trie = new PatriciaTrie();
            var key = 0.ToBytesForRLPEncoding();
            trie.Put(key, encodedTx);

            var proof = trie.GenerateProof(key);
            Assert.NotNull(proof);

            var verifyTrie = new PatriciaTrie(block.TransactionsHash);
            var retrieved = verifyTrie.Get(key, proof);

            Assert.NotNull(retrieved);
            Assert.True(encodedTx.SequenceEqual(retrieved),
                "Retrieved transaction should match original");
        }

        [Fact]
        public async Task AccountProof_CanBeGeneratedWithTrieNodeStore()
        {
            var tx = CreateSignedTransaction(_recipientAddress, BigInteger.Parse("100000000000000000"));
            await _node.SendTransactionAsync(tx);

            var block = await _node.GetLatestBlockAsync();
            Assert.NotNull(block);

            var proofService = new ProofService(_fixture.StateStore, _fixture.TrieNodeStore);
            var proof = await proofService.GenerateAccountProofAsync(
                _address,
                new List<BigInteger>(),
                block.StateRoot);

            Assert.NotNull(proof);
            Assert.Equal(_address.ToLower(), proof.Address.ToLower());
            Assert.NotNull(proof.AccountProofs);
            Assert.NotEmpty(proof.AccountProofs);
        }

        [Fact]
        public async Task AccountProof_ContainsCorrectBalance()
        {
            var transferAmount = BigInteger.Parse("100000000000000000");
            var tx = CreateSignedTransaction(_recipientAddress, transferAmount);
            await _node.SendTransactionAsync(tx);

            var block = await _node.GetLatestBlockAsync();

            var recipientBalance = await _node.GetBalanceAsync(_recipientAddress);

            var proofService = new ProofService(_fixture.StateStore, _fixture.TrieNodeStore);
            var proof = await proofService.GenerateAccountProofAsync(
                _recipientAddress,
                new List<BigInteger>(),
                block.StateRoot);

            Assert.NotNull(proof);
            Assert.Equal(recipientBalance, proof.Balance.Value);
        }

        [Fact]
        public async Task MultipleBlocks_AllHaveValidRoots()
        {
            for (int i = 0; i < 5; i++)
            {
                var tx = CreateSignedTransaction(_recipientAddress, BigInteger.Parse("10000000000000000"));
                var result = await _node.SendTransactionAsync(tx);
                Assert.True(result.Success);
            }

            var latestBlockNumber = await _node.GetBlockNumberAsync();
            Assert.True(latestBlockNumber >= 5);

            for (int i = 1; i <= 5; i++)
            {
                var block = await _node.GetBlockByNumberAsync(i);
                Assert.NotNull(block);
                Assert.NotNull(block.StateRoot);
                Assert.Equal(32, block.StateRoot.Length);
                Assert.NotNull(block.TransactionsHash);
                Assert.NotNull(block.ReceiptHash);
            }
        }

        [Fact]
        public async Task StateRoot_ChangesWithEachTransaction()
        {
            var stateRoots = new List<byte[]>();

            for (int i = 0; i < 3; i++)
            {
                var tx = CreateSignedTransaction(_recipientAddress, BigInteger.Parse("10000000000000000"));
                await _node.SendTransactionAsync(tx);

                var block = await _node.GetLatestBlockAsync();
                stateRoots.Add(block.StateRoot);
            }

            for (int i = 0; i < stateRoots.Count - 1; i++)
            {
                Assert.False(stateRoots[i].SequenceEqual(stateRoots[i + 1]),
                    $"State root {i} should differ from state root {i + 1}");
            }
        }

        [Fact]
        public async Task TrieNodes_CanBeLoadedLazily()
        {
            var tx = CreateSignedTransaction(_recipientAddress, BigInteger.Parse("100000000000000000"));
            await _node.SendTransactionAsync(tx);

            var block = await _node.GetLatestBlockAsync();
            Assert.NotNull(block);
            Assert.NotNull(block.StateRoot);

            var lazyTrie = PatriciaTrie.LoadFromStorage(block.StateRoot, _fixture.TrieNodeStore);
            Assert.NotNull(lazyTrie);
            Assert.NotNull(lazyTrie.Root);
        }

        [Fact]
        public async Task BlockHash_IsConsistentWithBlockHeader()
        {
            var tx = CreateSignedTransaction(_recipientAddress, BigInteger.Parse("100000000000000000"));
            await _node.SendTransactionAsync(tx);

            var block = await _node.GetLatestBlockAsync();
            Assert.NotNull(block);

            var encoder = BlockHeaderEncoder.Current;
            var encoded = encoder.Encode(block);
            var keccak = new Sha3Keccack();
            var computedHash = keccak.CalculateHash(encoded);

            var storedHash = await _fixture.BlockStore.GetHashByNumberAsync(block.BlockNumber);
            Assert.True(computedHash.SequenceEqual(storedHash),
                "Computed block hash should match stored hash");
        }

        [Fact]
        public async Task EmptyBlock_HasEmptyTrieRoots()
        {
            await _node.MineBlockAsync();

            var block = await _node.GetLatestBlockAsync();
            Assert.NotNull(block);

            Assert.True(block.TransactionsHash.SequenceEqual(DefaultValues.EMPTY_TRIE_HASH),
                "Empty block should have empty transactions root");
        }

        [Fact]
        public async Task ParentHash_LinksBlocksCorrectly()
        {
            var block1Hash = await _node.MineBlockAsync();

            var tx = CreateSignedTransaction(_recipientAddress, BigInteger.Parse("100000000000000000"));
            await _node.SendTransactionAsync(tx);

            var block2 = await _node.GetLatestBlockAsync();
            Assert.NotNull(block2);

            Assert.True(block1Hash.SequenceEqual(block2.ParentHash),
                "Block 2's parent hash should equal block 1's hash");
        }

        [Fact]
        public async Task StorageProof_CanBeGenerated()
        {
            var tx = CreateSignedTransaction(_recipientAddress, BigInteger.Parse("100000000000000000"));
            await _node.SendTransactionAsync(tx);

            var block = await _node.GetLatestBlockAsync();

            var proofService = new ProofService(_fixture.StateStore, _fixture.TrieNodeStore);
            var proof = await proofService.GenerateAccountProofAsync(
                _address,
                new List<BigInteger> { BigInteger.Zero },
                block.StateRoot);

            Assert.NotNull(proof);
            Assert.NotNull(proof.StorageProof);
        }

        [Fact]
        public async Task AccountProof_VerifiesCryptographically()
        {
            var transferAmount = BigInteger.Parse("100000000000000000");
            var tx = CreateSignedTransaction(_recipientAddress, transferAmount);
            await _node.SendTransactionAsync(tx);

            var block = await _node.GetLatestBlockAsync();

            var proofService = new ProofService(_fixture.StateStore, _fixture.TrieNodeStore);
            var proof = await proofService.GenerateAccountProofAsync(
                _recipientAddress,
                new List<BigInteger>(),
                block.StateRoot);

            Assert.NotNull(proof);
            Assert.NotEmpty(proof.AccountProofs);

            var proofBytes = proof.AccountProofs.Select(p => p.HexToByteArray()).ToList();

            var account = new Account
            {
                Balance = proof.Balance.Value,
                Nonce = proof.Nonce.Value,
                CodeHash = proof.CodeHash.HexToByteArray(),
                StateRoot = proof.StorageHash.HexToByteArray()
            };

            var isValid = AccountProofVerification.VerifyAccountProofs(
                _recipientAddress,
                block.StateRoot,
                proofBytes,
                account);

            Assert.True(isValid, "Account proof should verify against state root");
        }

        [Fact]
        public async Task AccountProof_InvalidProof_FailsVerification()
        {
            var tx = CreateSignedTransaction(_recipientAddress, BigInteger.Parse("100000000000000000"));
            await _node.SendTransactionAsync(tx);

            var block = await _node.GetLatestBlockAsync();

            var proofService = new ProofService(_fixture.StateStore, _fixture.TrieNodeStore);
            var proof = await proofService.GenerateAccountProofAsync(
                _recipientAddress,
                new List<BigInteger>(),
                block.StateRoot);

            var proofBytes = proof.AccountProofs.Select(p => p.HexToByteArray()).ToList();

            var wrongAccount = new Account
            {
                Balance = proof.Balance.Value + 1,
                Nonce = proof.Nonce.Value,
                CodeHash = proof.CodeHash.HexToByteArray(),
                StateRoot = proof.StorageHash.HexToByteArray()
            };

            var isValid = AccountProofVerification.VerifyAccountProofs(
                _recipientAddress,
                block.StateRoot,
                proofBytes,
                wrongAccount);

            Assert.False(isValid, "Proof with wrong balance should fail verification");
        }
    }
}
