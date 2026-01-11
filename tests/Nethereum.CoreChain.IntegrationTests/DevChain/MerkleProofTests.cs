using System.Numerics;
using Nethereum.CoreChain.IntegrationTests.Contracts;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.DevChain
{
    public class MerkleProofTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;
        private static readonly BigInteger OneToken = BigInteger.Parse("1000000000000000000");

        public MerkleProofTests(DevChainNodeFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task TransactionsRoot_IsNotEmptyAfterTransaction()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var block = await _fixture.Node.GetLatestBlockAsync();
            Assert.NotNull(block);
            Assert.NotNull(block.TransactionsHash);
            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH, block.TransactionsHash);
        }

        [Fact]
        public async Task TransactionsRoot_CanBeRecomputed()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));
            var encodedTx = signedTx.GetRLPEncoded();

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var block = await _fixture.Node.GetLatestBlockAsync();
            Assert.NotNull(block);

            var rootCalculator = new CoreChain.RootCalculator();
            var computedRoot = rootCalculator.CalculateTransactionsRoot(new List<byte[]> { encodedTx });

            Assert.Equal(block.TransactionsHash, computedRoot);
        }

        [Fact]
        public async Task TransactionsRoot_ProofGenerationAndVerification()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));
            var encodedTx = signedTx.GetRLPEncoded();

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var block = await _fixture.Node.GetLatestBlockAsync();
            Assert.NotNull(block);

            var trie = new PatriciaTrie();
            var key = 0.ToBytesForRLPEncoding();
            trie.Put(key, encodedTx);

            Assert.Equal(block.TransactionsHash, trie.Root.GetHash());

            var proof = trie.GenerateProof(key);
            Assert.NotNull(proof);

            var verifyTrie = new PatriciaTrie(block.TransactionsHash);
            var retrievedValue = verifyTrie.Get(key, proof);
            Assert.NotNull(retrievedValue);
            Assert.True(encodedTx.SequenceEqual(retrievedValue));
        }

        [Fact]
        public async Task ReceiptsRoot_IsNotEmptyAfterTransaction()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var block = await _fixture.Node.GetLatestBlockAsync();
            Assert.NotNull(block);
            Assert.NotNull(block.ReceiptHash);
            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH, block.ReceiptHash);
        }

        [Fact]
        public async Task ReceiptsRoot_CanBeRecomputed()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));

            var txResult = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(txResult.Success);
            Assert.NotNull(txResult.Receipt);

            var block = await _fixture.Node.GetLatestBlockAsync();
            Assert.NotNull(block);

            var rootCalculator = new CoreChain.RootCalculator();
            var computedRoot = rootCalculator.CalculateReceiptsRoot(new List<Receipt> { txResult.Receipt });

            Assert.Equal(block.ReceiptHash, computedRoot);
        }

        [Fact]
        public async Task StateRoot_ExistsAfterTransaction()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var blockAfter = await _fixture.Node.GetLatestBlockAsync();
            var stateRootAfter = blockAfter.StateRoot;

            Assert.NotNull(stateRootAfter);
            Assert.Equal(32, stateRootAfter.Length);
        }

        [Fact]
        public async Task MultipleTransactions_TransactionsRootIncludesAll()
        {
            await _fixture.Node.MineBlockAsync();

            var tx1 = _fixture.CreateSignedTransaction(_fixture.RecipientAddress, OneToken);
            var result1 = await _fixture.Node.SendTransactionAsync(tx1);
            Assert.True(result1.Success);

            var tx2 = _fixture.CreateSignedTransaction(_fixture.RecipientAddress, OneToken);
            var result2 = await _fixture.Node.SendTransactionAsync(tx2);
            Assert.True(result2.Success);

            var block = await _fixture.Node.GetLatestBlockAsync();
            Assert.NotNull(block.TransactionsHash);
            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH, block.TransactionsHash);
        }

        [Fact]
        public async Task ContractDeployment_TransactionsRootValid()
        {
            var bytecode = ERC20Contract.GetDeploymentBytecode();
            var deployTx = _fixture.CreateContractDeploymentTransaction(bytecode);
            var encodedTx = deployTx.GetRLPEncoded();

            var result = await _fixture.Node.SendTransactionAsync(deployTx);
            Assert.True(result.Success);

            var block = await _fixture.Node.GetLatestBlockAsync();
            Assert.NotNull(block);

            var trie = new PatriciaTrie();
            var key = 0.ToBytesForRLPEncoding();
            trie.Put(key, encodedTx);

            Assert.Equal(block.TransactionsHash, trie.Root.GetHash());
        }

        [Fact]
        public async Task ContractCall_ReceiptsRootContainsLogs()
        {
            var contractAddress = await _fixture.DeployERC20Async(OneToken * 1000);

            var result = await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, OneToken * 100);
            Assert.True(result.Success);
            Assert.NotEmpty(result.Logs);

            var block = await _fixture.Node.GetLatestBlockAsync();
            Assert.NotNull(block.ReceiptHash);
            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH, block.ReceiptHash);

            var rootCalculator = new CoreChain.RootCalculator();
            var computedRoot = rootCalculator.CalculateReceiptsRoot(new List<Receipt> { result.Receipt });

            Assert.Equal(block.ReceiptHash, computedRoot);
        }

        [Fact]
        public async Task EmptyBlock_HasEmptyTrieRoots()
        {
            await _fixture.Node.MineBlockAsync();
            var blockHash = await _fixture.Node.MineBlockAsync();

            var block = await _fixture.Node.GetBlockByHashAsync(blockHash);
            Assert.NotNull(block);

            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH, block.TransactionsHash);
        }

        [Fact]
        public async Task TransactionProof_VerifiesAgainstBlockHeader()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("200000000000000000"));
            var encodedTx = signedTx.GetRLPEncoded();

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var block = await _fixture.Node.GetLatestBlockAsync();

            var trie = new PatriciaTrie();
            var key = 0.ToBytesForRLPEncoding();
            trie.Put(key, encodedTx);

            var proof = trie.GenerateProof(key);
            Assert.NotNull(proof);

            var verifyTrie = new PatriciaTrie(block.TransactionsHash);
            var verified = verifyTrie.Get(key, proof);

            Assert.NotNull(verified);
            Assert.True(encodedTx.SequenceEqual(verified), "Transaction proof verification failed");
        }

        [Fact]
        public async Task ReceiptProof_VerifiesAgainstBlockHeader()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);
            Assert.NotNull(result.Receipt);

            var block = await _fixture.Node.GetLatestBlockAsync();
            var encodedReceipt = ReceiptEncoder.Current.Encode(result.Receipt);

            var trie = new PatriciaTrie();
            var key = 0.ToBytesForRLPEncoding();
            trie.Put(key, encodedReceipt);

            Assert.Equal(block.ReceiptHash, trie.Root.GetHash());

            var proof = trie.GenerateProof(key);
            Assert.NotNull(proof);

            var verifyTrie = new PatriciaTrie(block.ReceiptHash);
            var verified = verifyTrie.Get(key, proof);

            Assert.NotNull(verified);
            Assert.True(encodedReceipt.SequenceEqual(verified), "Receipt proof verification failed");
        }

        [Fact]
        public async Task InvalidProof_FailsVerification()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));
            var encodedTx = signedTx.GetRLPEncoded();

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var block = await _fixture.Node.GetLatestBlockAsync();

            var corruptedRoot = new byte[32];
            Array.Copy(block.TransactionsHash, corruptedRoot, 32);
            corruptedRoot[0] ^= 0xFF;

            var trie = new PatriciaTrie();
            var key = 0.ToBytesForRLPEncoding();
            trie.Put(key, encodedTx);

            var proof = trie.GenerateProof(key);

            var verifyTrie = new PatriciaTrie(corruptedRoot);
            var verified = verifyTrie.Get(key, proof);

            Assert.Null(verified);
        }

        [Fact]
        public async Task ConsecutiveBlocks_HaveStateRoots()
        {
            var block1Hash = await _fixture.Node.MineBlockAsync();
            var block1 = await _fixture.Node.GetBlockByHashAsync(block1Hash);

            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("500000000000000000"));
            await _fixture.Node.SendTransactionAsync(signedTx);

            var block2 = await _fixture.Node.GetLatestBlockAsync();

            Assert.NotNull(block1.StateRoot);
            Assert.NotNull(block2.StateRoot);
            Assert.Equal(32, block1.StateRoot.Length);
            Assert.Equal(32, block2.StateRoot.Length);
        }

        [Fact]
        public async Task Block_HasValidParentHash()
        {
            var block1Hash = await _fixture.Node.MineBlockAsync();

            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));
            await _fixture.Node.SendTransactionAsync(signedTx);

            var block2 = await _fixture.Node.GetLatestBlockAsync();

            Assert.NotNull(block2.ParentHash);
            Assert.True(block1Hash.SequenceEqual(block2.ParentHash));
        }

        [Fact]
        public async Task StateRoot_ChangesAfterTransaction()
        {
            var block1 = await _fixture.Node.GetLatestBlockAsync();
            var stateRoot1 = block1.StateRoot;

            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));
            await _fixture.Node.SendTransactionAsync(signedTx);

            var block2 = await _fixture.Node.GetLatestBlockAsync();
            var stateRoot2 = block2.StateRoot;

            Assert.NotNull(stateRoot1);
            Assert.NotNull(stateRoot2);
            Assert.False(stateRoot1.SequenceEqual(stateRoot2), "State root should change after transaction");
        }

        [Fact]
        public async Task StateRoot_IsNotEmptyTrieHash()
        {
            var block = await _fixture.Node.GetLatestBlockAsync();

            Assert.NotNull(block.StateRoot);
            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH, block.StateRoot);
        }

        [Fact]
        public async Task StateRoot_DifferentTransactionsProduceDifferentRoots()
        {
            await _fixture.Node.MineBlockAsync();
            var block1 = await _fixture.Node.GetLatestBlockAsync();
            var stateRoot1 = block1.StateRoot;

            var tx1 = _fixture.CreateSignedTransaction(_fixture.RecipientAddress, OneToken);
            await _fixture.Node.SendTransactionAsync(tx1);
            var block2 = await _fixture.Node.GetLatestBlockAsync();
            var stateRoot2 = block2.StateRoot;

            var tx2 = _fixture.CreateSignedTransaction(_fixture.RecipientAddress, OneToken * 2);
            await _fixture.Node.SendTransactionAsync(tx2);
            var block3 = await _fixture.Node.GetLatestBlockAsync();
            var stateRoot3 = block3.StateRoot;

            Assert.False(stateRoot1.SequenceEqual(stateRoot2));
            Assert.False(stateRoot2.SequenceEqual(stateRoot3));
            Assert.False(stateRoot1.SequenceEqual(stateRoot3));
        }

        [Fact]
        public async Task StateRoot_ContractStorageAffectsStateRoot()
        {
            var contractAddress = await _fixture.DeployERC20Async(OneToken * 1000);
            var block1 = await _fixture.Node.GetLatestBlockAsync();
            var stateRoot1 = block1.StateRoot;

            await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, OneToken * 100);
            var block2 = await _fixture.Node.GetLatestBlockAsync();
            var stateRoot2 = block2.StateRoot;

            Assert.NotNull(stateRoot1);
            Assert.NotNull(stateRoot2);
            Assert.False(stateRoot1.SequenceEqual(stateRoot2), "State root should change after contract storage modification");
        }
    }
}
