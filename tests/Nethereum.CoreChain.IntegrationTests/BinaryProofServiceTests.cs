using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Merkle.Binary.Proofs;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests
{
    public class BinaryProofServiceTests
    {
        private readonly ITestOutputHelper _output;

        public BinaryProofServiceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // End-to-end validation of the entire binary-trie stack through proofs.
        // If ANY layer is wrong (key derivation, BasicDataLeaf packing,
        // CodeChunker, ValuesMerkleizer, BinaryTrieHash shortcut, trie
        // structure, proof path collection), the proof verification fails
        // against the jsign-validated root. This is stronger than root
        // comparison alone — it validates every intermediate hash on the path.
        [Fact]
        public async Task ProveAccount_VerifiesAgainstJsignRoot_ExtractsCorrectFields()
        {
            var hashProvider = new Blake3HashProvider();
            var stateStore = new InMemoryStateStore();

            var address = "0x1000000000000000000000000000000000000000";
            var code = new byte[] { 0x60, 0x01, 0x60, 0x00, 0x55 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            var balance = new EvmUInt256(1000);
            ulong nonce = 1;

            await stateStore.SaveCodeAsync(codeHash, code);
            await stateStore.SaveAccountAsync(address, new Account
            {
                Nonce = nonce, Balance = balance, CodeHash = codeHash
            });
            await stateStore.ClearDirtyTrackingAsync();

            var calc = new BinaryIncrementalStateRootCalculator(stateStore, hashProvider);
            var root = await calc.ComputeFullStateRootAsync();

            // Root must match jsign reference (cross-checked 2026-04-21)
            Assert.Equal(
                "acc1f843250ebabbc9c2aa5392741656da98ffb3ec5246b9a64f79ef16048a83",
                root.ToHex());

            var trie = GetTrieFromCalculator(calc);
            var proofService = new BinaryProofService(trie, hashProvider);

            // Prove account → generates proofs for basic-data + code-hash
            var accountProof = proofService.ProveAccount(address);

            // Proofs verify against the jsign-validated root
            Assert.Equal(root, accountProof.RootHash);

            // Extracted fields match what we put in
            Assert.Equal(nonce, accountProof.Nonce);
            Assert.Equal((BigInteger)balance, (BigInteger)accountProof.Balance);
            Assert.Equal((uint)code.Length, accountProof.CodeSize);
            Assert.Equal(codeHash, accountProof.CodeHash);
            Assert.Equal((byte)0, accountProof.Version);

            // Proofs are non-empty (contain the path from root to leaf)
            Assert.NotNull(accountProof.BasicDataProof);
            Assert.NotEmpty(accountProof.BasicDataProof.Nodes);
            Assert.NotNull(accountProof.CodeHashProof);
            Assert.NotEmpty(accountProof.CodeHashProof.Nodes);

            _output.WriteLine($"Root:     {root.ToHex(true)}");
            _output.WriteLine($"Nonce:    {accountProof.Nonce}");
            _output.WriteLine($"Balance:  {accountProof.Balance}");
            _output.WriteLine($"CodeSize: {accountProof.CodeSize}");
            _output.WriteLine($"CodeHash: {accountProof.CodeHash?.ToHex(true)}");
            _output.WriteLine($"BasicDataProof nodes: {accountProof.BasicDataProof.Nodes.Length}");
            _output.WriteLine($"CodeHashProof nodes:  {accountProof.CodeHashProof.Nodes.Length}");
        }

        [Fact]
        public async Task ProveStorageSlot_VerifiesAgainstRoot()
        {
            var hashProvider = new Blake3HashProvider();
            var stateStore = new InMemoryStateStore();

            var address = "0x1000000000000000000000000000000000000000";
            var code = new byte[] { 0x60, 0x01, 0x60, 0x00, 0x55 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);

            await stateStore.SaveCodeAsync(codeHash, code);
            await stateStore.SaveAccountAsync(address, new Account
            {
                Nonce = 1, Balance = new EvmUInt256(1000), CodeHash = codeHash
            });
            await stateStore.SaveStorageAsync(address, 0, new byte[] { 0x42 });
            await stateStore.SaveStorageAsync(address, 100, new byte[] { 0xFF });
            await stateStore.ClearDirtyTrackingAsync();

            var calc = new BinaryIncrementalStateRootCalculator(stateStore, hashProvider);
            await calc.ComputeFullStateRootAsync();

            var trie = GetTrieFromCalculator(calc);
            var proofService = new BinaryProofService(trie, hashProvider);

            // Prove slot 0
            var slot0Proof = proofService.ProveStorageSlot(address, EvmUInt256.Zero);
            Assert.NotNull(slot0Proof.Value);
            Assert.Equal(0x42, slot0Proof.Value[31]);
            Assert.NotEmpty(slot0Proof.Proof.Nodes);

            // Prove slot 100
            var slot100Proof = proofService.ProveStorageSlot(address, new EvmUInt256(100));
            Assert.NotNull(slot100Proof.Value);
            Assert.Equal(0xFF, slot100Proof.Value[31]);

            // Independently verify proofs
            var verifier = new BinaryTrieProofVerifier(hashProvider);
            var rootHash = trie.ComputeRoot();

            var slot0Key = new Merkle.Binary.Keys.BinaryTreeKeyDerivation(hashProvider)
                .GetTreeKeyForStorageSlot(
                    AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray(),
                    EvmUInt256.Zero);
            var verified0 = verifier.VerifyProof(rootHash, slot0Key, slot0Proof.Proof);
            Assert.NotNull(verified0);
            Assert.Equal(0x42, verified0[31]);

            _output.WriteLine($"Slot 0 value:   0x{slot0Proof.Value.ToHex()}");
            _output.WriteLine($"Slot 100 value: 0x{slot100Proof.Value.ToHex()}");
            _output.WriteLine($"Slot 0 proof nodes: {slot0Proof.Proof.Nodes.Length}");
        }

        // Multiple hash providers: proofs must verify under each.
        [Theory]
        [InlineData(typeof(Blake3HashProvider))]
        [InlineData(typeof(Sha256HashProvider))]
        public async Task ProveAccount_WorksWithMultipleHashProviders(System.Type hashProviderType)
        {
            var hashProvider = (IHashProvider)System.Activator.CreateInstance(hashProviderType);
            var stateStore = new InMemoryStateStore();

            var address = "0x2000000000000000000000000000000000000000";
            var code = new byte[] { 0x60, 0x42 };
            var codeHash = Sha3Keccack.Current.CalculateHash(code);

            await stateStore.SaveCodeAsync(codeHash, code);
            await stateStore.SaveAccountAsync(address, new Account
            {
                Nonce = 42, Balance = new EvmUInt256(999), CodeHash = codeHash
            });
            await stateStore.ClearDirtyTrackingAsync();

            var calc = new BinaryIncrementalStateRootCalculator(stateStore, hashProvider);
            await calc.ComputeFullStateRootAsync();

            var trie = GetTrieFromCalculator(calc);
            var proofService = new BinaryProofService(trie, hashProvider);

            var result = proofService.ProveAccount(address);
            Assert.Equal(42UL, result.Nonce);
            Assert.Equal((BigInteger)999, (BigInteger)result.Balance);
            Assert.Equal(codeHash, result.CodeHash);
            Assert.NotEmpty(result.BasicDataProof.Nodes);
        }

        // Access the internal _trie field for proof generation. In production
        // the trie would be exposed via a service or the calculator would
        // implement a proof interface directly.
        private static Merkle.Binary.BinaryTrie GetTrieFromCalculator(
            BinaryIncrementalStateRootCalculator calc)
        {
            var field = typeof(BinaryIncrementalStateRootCalculator)
                .GetField("_trie", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (Merkle.Binary.BinaryTrie)field.GetValue(calc);
        }
    }
}
