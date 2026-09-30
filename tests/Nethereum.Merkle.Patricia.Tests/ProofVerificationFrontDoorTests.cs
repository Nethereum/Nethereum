using System.Collections.Generic;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests
{
    using ProofVerification = Nethereum.Merkle.Patricia.ProofVerification.ProofVerification;

    public class ProofVerificationFrontDoorTests
    {
        private const string TargetAddress = "0x12890d2cce102216644c59dae5baed380d84830c";

        [NethereumDocExample(DocSection.ChainInfrastructure, "quick-start", "Quick start: compute a root, prove a key against it, and verify the proof", Order = 1)]
        [Fact]
        public void QuickStart_ComputeARootProveAKeyAndVerifyIt()
        {
            var hashProvider = Sha3KeccackHashProvider.Instance;
            var trie = new PatriciaTrie(new InMemoryContentNodeStore());

            for (var i = 0; i < 128; i++)
                trie.Put(hashProvider.ComputeHash(new[] { (byte)i }), new byte[] { (byte)i, 0xAB });
            trie.SaveNodesToStorage();

            var root = trie.Root.GetHash();
            var key = hashProvider.ComputeHash(new byte[] { 7 });
            var proof = ProofGenerator.GenerateProof(trie, key);

            Assert.Equal(root, hashProvider.ComputeHash(proof[0]));
            Assert.True(ProofVerification.Current.Range.VerifyEntry(root, key, new byte[] { 7, 0xAB }, proof));
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "patricia-trie", "Build a trie, commit it, and reload it by root - all through one held store", Order = 3)]
        [Fact]
        public void BuildCommitAndReloadThroughOneHeldStore()
        {
            var keccak = new Sha3Keccack();
            var store = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(store);

            var keys = new List<byte[]>();
            var values = new List<byte[]>();
            for (var i = 0; i < 200; i++)
            {
                var key = keccak.CalculateHash(new byte[] { (byte)i, (byte)(i >> 8) });
                var value = Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i });
                keys.Add(key);
                values.Add(value);
                trie.Put(key, value);
            }
            trie.SaveNodesToStorage();

            var rootHash = trie.Root.GetHash();
            var reloaded = new PatriciaTrie(rootHash, store);

            for (var i = 0; i < keys.Count; i++)
                Assert.Equal(values[i], reloaded.Get(keys[i]));
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "state-proofs", "Verify an account against a state root with the account proof verifier", Order = 2)]
        [Fact]
        public void AccountProof_VerifiesAgainstTheStateRoot()
        {
            var hashProvider = Sha3KeccackHashProvider.Instance;
            var accountTrie = new PatriciaTrie(new InMemoryContentNodeStore());

            var account = new Account { Nonce = 7, Balance = 1234567 };
            var accountKey = hashProvider.ComputeHash(TargetAddress.HexToByteArray());
            accountTrie.Put(accountKey, AccountEncoder.Current.Encode(account));
            for (var i = 0; i < 64; i++)
                accountTrie.Put(hashProvider.ComputeHash(new[] { (byte)i, (byte)0xAA }),
                                AccountEncoder.Current.Encode(new Account { Nonce = (ulong)i }));
            accountTrie.SaveNodesToStorage();

            var stateRoot = accountTrie.Root.GetHash();
            var proof = ProofGenerator.GenerateProof(accountTrie, accountKey);

            Assert.True(ProofVerification.Current.Account.Verify(stateRoot, proof, TargetAddress, account));
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "state-proofs", "Reject an account proof whose claimed balance does not match the committed state", Order = 3)]
        [Fact]
        public void AccountProof_WithATamperedBalance_IsRejected()
        {
            var hashProvider = Sha3KeccackHashProvider.Instance;
            var accountTrie = new PatriciaTrie(new InMemoryContentNodeStore());

            var account = new Account { Nonce = 7, Balance = 1234567 };
            var accountKey = hashProvider.ComputeHash(TargetAddress.HexToByteArray());
            accountTrie.Put(accountKey, AccountEncoder.Current.Encode(account));
            accountTrie.SaveNodesToStorage();

            var proof = ProofGenerator.GenerateProof(accountTrie, accountKey);
            var lying = new Account { Nonce = 7, Balance = 9999999 };

            Assert.False(ProofVerification.Current.Account.Verify(
                accountTrie.Root.GetHash(), proof, TargetAddress, lying));
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "state-proofs", "Verify a contract storage slot, and see that an absent key yields no proof at all", Order = 4)]
        [Fact]
        public void StorageProof_VerifiesASetSlot_AndAnAbsentKeyYieldsNoProof()
        {
            var hashProvider = Sha3KeccackHashProvider.Instance;
            var storageTrie = new PatriciaTrie(new InMemoryContentNodeStore());

            var slot = new byte[] { 0x01 };
            var slotValue = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            for (var i = 1; i <= 32; i++)
                storageTrie.Put(AccountStorage.EncodeKeyForStorage(new[] { (byte)i }, hashProvider),
                                AccountStorage.EncodeValueForStorage(new byte[] { (byte)i, 0x01 }));
            storageTrie.Put(AccountStorage.EncodeKeyForStorage(slot, hashProvider),
                            AccountStorage.EncodeValueForStorage(slotValue));
            storageTrie.SaveNodesToStorage();

            var storageRoot = storageTrie.Root.GetHash();
            var inclusion = ProofGenerator.GenerateProof(
                storageTrie, AccountStorage.EncodeKeyForStorage(slot, hashProvider));

            Assert.True(ProofVerification.Current.Storage.Verify(storageRoot, inclusion, slot, slotValue));

            var unsetSlot = new byte[] { 0x7F };
            Assert.Null(ProofGenerator.GenerateProof(
                storageTrie, AccountStorage.EncodeKeyForStorage(unsetSlot, hashProvider)));
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "key-path-storage", "Check a raw node blob against its reference hash - the read-time integrity check a path store makes", Order = 3)]
        [Fact]
        public void TrieNodeVerifier_AcceptsTheAuthenticBlobAndRejectsATamperedOne()
        {
            var hashProvider = Sha3KeccackHashProvider.Instance;
            var trie = new PatriciaTrie(new InMemoryContentNodeStore());
            for (var i = 0; i < 40; i++)
                trie.Put(hashProvider.ComputeHash(new[] { (byte)i }), new byte[] { (byte)i, 0x02 });
            trie.SaveNodesToStorage();

            var blob = trie.Root.GetEncodedData();
            var referenceHash = trie.Root.GetHash();

            Assert.True(ProofVerification.Current.TrieNode.Verify(referenceHash, blob, hashProvider));

            var tampered = (byte[])blob.Clone();
            tampered[tampered.Length - 1] ^= 0xFF;
            Assert.False(ProofVerification.Current.TrieNode.Verify(referenceHash, tampered, hashProvider));
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "snap-range-proofs", "Walk a trie range lexicographically with PatriciaRangeIterator", Order = 2)]
        [Fact]
        public void RangeIterator_WalksTheTrieInKeyOrderFromTheStartKey()
        {
            var hashProvider = Sha3KeccackHashProvider.Instance;
            var store = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(store);
            for (var i = 0; i < 128; i++)
                trie.Put(hashProvider.ComputeHash(new[] { (byte)i, (byte)(i >> 8) }),
                         new byte[] { (byte)i, 0xCD });
            trie.SaveNodesToStorage();

            var page = new List<PatriciaRangeIterator.RangeEntry>(
                PatriciaRangeIterator.EnumerateRange(trie.Root, store, new byte[32], maxCount: 16));

            Assert.Equal(16, page.Count);
            for (var i = 0; i + 1 < page.Count; i++)
                Assert.True(ByteArrayComparer.Current.Compare(page[i].KeyBytes, page[i + 1].KeyBytes) < 0);
        }
    }
}
