using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Services
{
    public class ProofServiceIntegrationTests : IAsyncLifetime
    {
        private DevChainNode _node;
        private InMemoryBlockStore _blockStore;
        private InMemoryStateStore _stateStore;
        private InMemoryTrieNodeStore _trieNodeStore;

        private readonly string _privateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private readonly string _address = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private readonly BigInteger _chainId = 31337;
        private readonly LegacyTransactionSigner _signer = new();

        private const string ContractAddress = "0x1111111111111111111111111111111111111111";

        public async Task InitializeAsync()
        {
            _blockStore = new InMemoryBlockStore();
            _stateStore = new InMemoryStateStore();
            _trieNodeStore = new InMemoryTrieNodeStore();

            var config = new DevChainConfig
            {
                ChainId = _chainId,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            };

            _node = new DevChainNode(
                config,
                _blockStore,
                new InMemoryTransactionStore(_blockStore),
                new InMemoryReceiptStore(),
                new InMemoryLogStore(),
                _stateStore,
                new InMemoryFilterStore(),
                _trieNodeStore);

            await _node.StartAsync(new[] { _address }, BigInteger.Parse("10000000000000000000000"));

            await _node.SetCodeAsync(ContractAddress, new byte[] { 0x00 });
            await _node.SetStorageAtAsync(ContractAddress, BigInteger.Zero,
                new BigInteger(100).ToByteArray(isUnsigned: true, isBigEndian: true));
            await _node.SetStorageAtAsync(ContractAddress, BigInteger.One,
                new BigInteger(200).ToByteArray(isUnsigned: true, isBigEndian: true));
            await _node.SetStorageAtAsync(ContractAddress, new BigInteger(2),
                new BigInteger(300).ToByteArray(isUnsigned: true, isBigEndian: true));

            await _node.MineBlockAsync();
        }

        public Task DisposeAsync()
        {
            _node?.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task StorageTrieNodes_PersistedDuringBlockProduction()
        {
            var block = await _node.GetLatestBlockAsync();
            Assert.NotNull(block.StateRoot);
            Assert.Equal(32, block.StateRoot.Length);

            var stateRootNode = _trieNodeStore.Get(block.StateRoot);
            Assert.NotNull(stateRootNode);

            var account = await _stateStore.GetAccountAsync(ContractAddress);
            Assert.NotNull(account);
            Assert.NotNull(account.StateRoot);
            Assert.False(account.StateRoot.SequenceEqual(DefaultValues.EMPTY_TRIE_HASH),
                "Contract with storage should not have empty trie hash as storage root");

            var storageRootNode = _trieNodeStore.Get(account.StateRoot);
            Assert.NotNull(storageRootNode);
        }

        [Fact]
        public async Task StorageProof_LoadsFromPersistedTrieNodes()
        {
            var account = await _stateStore.GetAccountAsync(ContractAddress);

            var storageTrie = PatriciaTrie.LoadFromStorage(account.StateRoot, _trieNodeStore);
            Assert.NotNull(storageTrie);
            Assert.NotNull(storageTrie.Root);
            Assert.False(storageTrie.Root is EmptyNode,
                "Storage trie loaded from persistent nodes should not be empty");

            var sha3 = new Sha3Keccack();
            var slotBytes = BigInteger.Zero.ToBytesForRLPEncoding().PadBytes(32);
            var hashedSlot = sha3.CalculateHash(slotBytes);

            var proof = storageTrie.GenerateProof(hashedSlot, _trieNodeStore);
            Assert.NotNull(proof);
            Assert.NotNull(proof.Storage);

            var proofNodes = proof.Storage.Values.Where(p => p != null).ToList();
            Assert.NotEmpty(proofNodes);
        }

        [Fact]
        public async Task StorageProof_ValuesMatchContractStorage()
        {
            var block = await _node.GetLatestBlockAsync();

            var proofService = new ProofService(_stateStore, _trieNodeStore);
            var result = await proofService.GenerateAccountProofAsync(
                ContractAddress,
                new List<BigInteger> { BigInteger.Zero, BigInteger.One, new BigInteger(2) },
                block.StateRoot);

            Assert.NotNull(result);
            Assert.Equal(3, result.StorageProof.Count);

            Assert.Equal(new BigInteger(100), result.StorageProof[0].Value.Value);
            Assert.Equal(new BigInteger(200), result.StorageProof[1].Value.Value);
            Assert.Equal(new BigInteger(300), result.StorageProof[2].Value.Value);

            foreach (var sp in result.StorageProof)
            {
                Assert.NotNull(sp.Proof);
                Assert.NotEmpty(sp.Proof);
            }
        }

        [Fact]
        public async Task StorageProof_VerifiesCryptographicallyViaTrieReconstruction()
        {
            var block = await _node.GetLatestBlockAsync();
            var account = await _stateStore.GetAccountAsync(ContractAddress);

            var proofService = new ProofService(_stateStore, _trieNodeStore);
            var result = await proofService.GenerateAccountProofAsync(
                ContractAddress,
                new List<BigInteger> { BigInteger.Zero, BigInteger.One, new BigInteger(2) },
                block.StateRoot);

            var sha3 = new Sha3Keccack();
            var sha3Provider = new Nethereum.Util.HashProviders.Sha3KeccackHashProvider();
            var expectedValues = new[] { 100, 200, 300 };

            for (int i = 0; i < 3; i++)
            {
                var sp = result.StorageProof[i];
                var proofBytes = sp.Proof.Select(p => p.HexToByteArray()).ToList();

                var verifyTrie = new PatriciaTrie(account.StateRoot);
                var inMemoryStorage = new InMemoryTrieStorage();

                foreach (var proofItem in proofBytes)
                {
                    inMemoryStorage.Put(sha3Provider.ComputeHash(proofItem), proofItem);
                }

                var slotBytes = sp.Key.Value.ToBytesForRLPEncoding().PadBytes(32);
                var hashedSlot = sha3.CalculateHash(slotBytes);

                var valueFromTrie = verifyTrie.Get(hashedSlot, inMemoryStorage);
                Assert.NotNull(valueFromTrie);

                var expectedValueBytes = TrimLeadingZeros(
                    new BigInteger(expectedValues[i]).ToByteArray(isUnsigned: true, isBigEndian: true));
                var expectedEncoded = RLP.RLP.EncodeElement(expectedValueBytes);
                Assert.True(valueFromTrie.SequenceEqual(expectedEncoded),
                    $"Storage proof for slot {i} should verify: expected value {expectedValues[i]}");

                Assert.True(verifyTrie.Root.GetHash().SequenceEqual(account.StateRoot),
                    $"Storage trie root should match account's storage root for slot {i}");
            }
        }

        [Fact]
        public async Task AccountProof_WithStorageProof_BothVerifyCryptographically()
        {
            var block = await _node.GetLatestBlockAsync();

            var proofService = new ProofService(_stateStore, _trieNodeStore);
            var result = await proofService.GenerateAccountProofAsync(
                ContractAddress,
                new List<BigInteger> { BigInteger.Zero },
                block.StateRoot);

            Assert.NotNull(result);
            Assert.NotEmpty(result.AccountProofs);
            Assert.NotEmpty(result.StorageProof);

            var accountProofBytes = result.AccountProofs.Select(p => p.HexToByteArray()).ToList();
            var accountForVerify = new Account
            {
                Balance = result.Balance.Value,
                Nonce = result.Nonce.Value,
                CodeHash = result.CodeHash.HexToByteArray(),
                StateRoot = result.StorageHash.HexToByteArray()
            };

            var accountValid = AccountProofVerification.VerifyAccountProofs(
                ContractAddress,
                block.StateRoot,
                accountProofBytes,
                accountForVerify);
            Assert.True(accountValid, "Account proof should verify cryptographically against block state root");

            var storageRoot = result.StorageHash.HexToByteArray();
            Assert.False(storageRoot.SequenceEqual(DefaultValues.EMPTY_TRIE_HASH),
                "Contract with storage should have non-empty storage hash");

            var sp = result.StorageProof[0];
            Assert.NotEmpty(sp.Proof);
            Assert.Equal(new BigInteger(100), sp.Value.Value);
        }

        [Fact]
        public async Task StorageProof_NonExistentSlot_ReturnsZeroValue()
        {
            var block = await _node.GetLatestBlockAsync();

            var proofService = new ProofService(_stateStore, _trieNodeStore);
            var result = await proofService.GenerateAccountProofAsync(
                ContractAddress,
                new List<BigInteger> { new BigInteger(999) },
                block.StateRoot);

            Assert.Single(result.StorageProof);
            var sp = result.StorageProof[0];
            Assert.Equal(BigInteger.Zero, sp.Value.Value);
            Assert.NotNull(sp.Proof);
        }

        [Fact]
        public async Task StorageHash_MatchesAccountStateRoot()
        {
            var block = await _node.GetLatestBlockAsync();
            var account = await _stateStore.GetAccountAsync(ContractAddress);

            var proofService = new ProofService(_stateStore, _trieNodeStore);
            var result = await proofService.GenerateAccountProofAsync(
                ContractAddress,
                new List<BigInteger>(),
                block.StateRoot);

            Assert.Equal(account.StateRoot.ToHex(true), result.StorageHash);
        }

        [Fact]
        public async Task MultipleBlocks_StorageProofsRemainValid()
        {
            var recipient = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
            for (int i = 0; i < 3; i++)
            {
                var nonce = await _node.GetNonceAsync(_address);
                var signedTxHex = _signer.SignTransaction(
                    _privateKey.HexToByteArray(),
                    _chainId,
                    recipient,
                    BigInteger.Parse("10000000000000000"),
                    nonce,
                    1_000_000_000,
                    21_000,
                    "");
                var tx = TransactionFactory.CreateTransaction(signedTxHex);
                var txResult = await _node.SendTransactionAsync(tx);
                Assert.True(txResult.Success);
            }

            var block = await _node.GetLatestBlockAsync();
            Assert.True(block.BlockNumber >= 4);

            var proofService = new ProofService(_stateStore, _trieNodeStore);
            var result = await proofService.GenerateAccountProofAsync(
                ContractAddress,
                new List<BigInteger> { BigInteger.Zero, BigInteger.One },
                block.StateRoot);

            Assert.Equal(new BigInteger(100), result.StorageProof[0].Value.Value);
            Assert.Equal(new BigInteger(200), result.StorageProof[1].Value.Value);
            Assert.NotEmpty(result.StorageProof[0].Proof);
            Assert.NotEmpty(result.StorageProof[1].Proof);

            var accountProofBytes = result.AccountProofs.Select(p => p.HexToByteArray()).ToList();
            var accountForVerify = new Account
            {
                Balance = result.Balance.Value,
                Nonce = result.Nonce.Value,
                CodeHash = result.CodeHash.HexToByteArray(),
                StateRoot = result.StorageHash.HexToByteArray()
            };

            var accountValid = AccountProofVerification.VerifyAccountProofs(
                ContractAddress,
                block.StateRoot,
                accountProofBytes,
                accountForVerify);
            Assert.True(accountValid, "Account proof should still verify after multiple blocks");
        }

        private static byte[] TrimLeadingZeros(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return new byte[0];

            var firstNonZero = 0;
            while (firstNonZero < bytes.Length && bytes[firstNonZero] == 0)
                firstNonZero++;

            if (firstNonZero == bytes.Length)
                return new byte[0];

            var result = new byte[bytes.Length - firstNonZero];
            System.Array.Copy(bytes, firstNonZero, result, 0, result.Length);
            return result;
        }
    }
}
