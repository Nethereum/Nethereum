using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Hex.HexTypes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Proofs;
namespace Nethereum.CoreChain.Services
{
    public class ProofService : IProofService
    {
        private readonly IStateStore _stateStore;
        private readonly ITrieNodeStore _trieNodeStore;
        private readonly RootCalculator _rootCalculator;
        private static readonly byte[] AbsentAccountHash = new byte[32];

        public ProofService(IStateStore stateStore, ITrieNodeStore trieNodeStore = null)
        {
            _stateStore = stateStore;
            _trieNodeStore = trieNodeStore;
            _rootCalculator = new RootCalculator();
        }

        public async Task<AccountProof> GenerateAccountProofAsync(
            string address,
            List<BigInteger> storageKeys,
            byte[] stateRoot)
        {
            var addressHash = StateKeys.AccountKey(address);

            bool exists, trieBacked;
            BigInteger nonce, balance;
            byte[] codeHash, accountStorageRoot;
            List<string> accountProofHex;

            if (HasWalkableRoot(stateRoot))
            {
                RejectRootWithoutRetainedNodes(stateRoot);

                var trie = PatriciaTrie.LoadFromStorage(stateRoot, _trieNodeStore);

                var accountRlp = ReadLeaf(trie, addressHash, stateRoot);
                var decoded = accountRlp != null ? AccountEncoder.Current.Decode(accountRlp) : null;
                accountProofHex = GenerateProofHex(trie, addressHash, stateRoot);

                exists = decoded != null;
                nonce = decoded?.Nonce ?? BigInteger.Zero;
                balance = decoded?.Balance ?? BigInteger.Zero;
                codeHash = decoded?.CodeHash ?? DefaultValues.EMPTY_DATA_HASH;
                accountStorageRoot = decoded?.StateRoot ?? DefaultValues.EMPTY_TRIE_HASH;
                trieBacked = true;
            }
            else
            {
                var account = await _stateStore.GetAccountAsync(address);
                accountProofHex = await GenerateAccountProofWithFullRebuildAsync(addressHash, stateRoot);

                exists = account != null;
                nonce = account?.Nonce ?? BigInteger.Zero;
                balance = account?.Balance ?? BigInteger.Zero;
                codeHash = account?.CodeHash ?? DefaultValues.EMPTY_DATA_HASH;
                accountStorageRoot = account?.StateRoot;
                trieBacked = false;
            }

            var storageHash = await ResolveStorageHashAsync(address, accountStorageRoot, trieBacked);

            var storageProofs = new List<StorageProof>();
            if (storageKeys != null && storageKeys.Count > 0)
            {
                storageProofs = await GenerateStorageProofsAsync(address, storageKeys, accountStorageRoot, stateRoot);
            }

            return new AccountProof
            {
                Address = address,
                Balance = new HexBigInteger(balance),
                CodeHash = (exists ? codeHash : AbsentAccountHash).ToHex(true),
                Nonce = new HexBigInteger(nonce),
                StorageHash = (exists ? storageHash : AbsentAccountHash).ToHex(true),
                AccountProofs = accountProofHex,
                StorageProof = storageProofs
            };
        }

        private void RejectRootWithoutRetainedNodes(byte[] stateRoot)
        {
            if (!_trieNodeStore.ContainsKey(stateRoot))
                throw new StateNotAvailableException(stateRoot);
        }

        private async Task<byte[]> ResolveStorageHashAsync(string address, byte[] storageRoot, bool trieBacked)
        {
            byte[] storageHash = DefaultValues.EMPTY_TRIE_HASH;

            if (trieBacked)
            {
                storageHash = (storageRoot != null && storageRoot.Length == 32)
                    ? storageRoot
                    : DefaultValues.EMPTY_TRIE_HASH;
            }
            else if (IsNonEmptyRoot(storageRoot))
            {
                storageHash = storageRoot;
            }
            else
            {
                var accountStorage = await _stateStore.GetAllStorageAsync(address);
                if (accountStorage.Count > 0)
                {
                    storageHash = _rootCalculator.CalculateStorageRoot(accountStorage, _trieNodeStore);
                }
            }

            return storageHash;
        }

        private async Task<List<string>> GenerateAccountProofWithFullRebuildAsync(byte[] addressHash, byte[] stateRoot)
        {
            var accounts = await _stateStore.GetAllAccountsAsync();

            ITrieNodeStore nodeStore = _trieNodeStore ?? new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(nodeStore);

            foreach (var kvp in accounts)
            {
                if (kvp.Value == null) continue;

                var hashedKey = StateKeys.AccountKey(kvp.Key);

                var storage = await _stateStore.GetAllStorageAsync(kvp.Key);
                var acc = new Account
                {
                    Nonce = kvp.Value.Nonce,
                    Balance = kvp.Value.Balance,
                    CodeHash = kvp.Value.CodeHash ?? DefaultValues.EMPTY_DATA_HASH
                };

                if (storage.Count > 0)
                {
                    acc.StateRoot = _rootCalculator.CalculateStorageRoot(storage, _trieNodeStore);
                }
                else
                {
                    acc.StateRoot = DefaultValues.EMPTY_TRIE_HASH;
                }

                var encodedAccount = AccountEncoder.Current.Encode(acc);
                trie.Put(hashedKey, encodedAccount);
            }

            return GenerateProofHex(trie, addressHash, stateRoot);
        }

        private async Task<List<StorageProof>> GenerateStorageProofsAsync(
            string address,
            List<BigInteger> storageKeys,
            byte[] storageRoot,
            byte[] stateRoot)
        {
            var proofs = new List<StorageProof>();

            if (HasWalkableRoot(storageRoot))
            {
                AddProofsFromWalkedStorageRoot(proofs, address, storageKeys, storageRoot, stateRoot);
                return proofs;
            }

            if (_trieNodeStore != null)
            {
                AddZeroValuedProofs(proofs, storageKeys);
                return proofs;
            }

            var storage = await _stateStore.GetAllStorageAsync(address);

            if (storage.Count == 0)
            {
                AddZeroValuedProofs(proofs, storageKeys);
                return proofs;
            }

            AddProofsFromRebuiltStorageTrie(proofs, storageKeys, storage, stateRoot);
            return proofs;
        }

        private void AddProofsFromWalkedStorageRoot(
            List<StorageProof> proofs,
            string address,
            List<BigInteger> storageKeys,
            byte[] storageRoot,
            byte[] stateRoot)
        {
            var owner = StateKeys.AccountKey(address);
            var storageTrie = PatriciaTrie.LoadFromStorage(storageRoot, _trieNodeStore, owner);

            foreach (var key in storageKeys)
            {
                var hashedSlot = StateKeys.StorageSlotKey(key);

                var proofHex = GenerateProofHex(storageTrie, hashedSlot, stateRoot);

                var storageLeaf = ReadLeaf(storageTrie, hashedSlot, stateRoot);
                var valueBigInt = storageLeaf != null
                    ? RLP.RLP.Decode(storageLeaf).RLPData.ToBigIntegerFromRLPDecoded()
                    : BigInteger.Zero;

                proofs.Add(new StorageProof
                {
                    Key = new HexBigInteger(key),
                    Value = new HexBigInteger(valueBigInt),
                    Proof = proofHex
                });
            }
        }

        private void AddProofsFromRebuiltStorageTrie(
            List<StorageProof> proofs,
            List<BigInteger> storageKeys,
            Dictionary<byte[], byte[]> storage,
            byte[] stateRoot)
        {
            ITrieNodeStore storageNodeStore = new InMemoryContentNodeStore();
            var rebuildTrie = new PatriciaTrie(storageNodeStore);

            foreach (var kvp in storage)
            {
                if (kvp.Value == null) continue;
                var encodedValue = RLP.RLP.EncodeElement(kvp.Value);
                rebuildTrie.Put(kvp.Key, encodedValue);
            }

            foreach (var key in storageKeys)
            {
                var hashedSlot = StateKeys.StorageSlotKey(key);

                var proofHex = GenerateProofHex(rebuildTrie, hashedSlot, stateRoot);

                storage.TryGetValue(hashedSlot, out var value);
                var valueBigInt = value != null ? value.ToBigIntegerFromRLPDecoded() : BigInteger.Zero;

                proofs.Add(new StorageProof
                {
                    Key = new HexBigInteger(key),
                    Value = new HexBigInteger(valueBigInt),
                    Proof = proofHex
                });
            }
        }

        private static void AddZeroValuedProofs(List<StorageProof> proofs, List<BigInteger> storageKeys)
        {
            foreach (var key in storageKeys)
            {
                proofs.Add(new StorageProof
                {
                    Key = new HexBigInteger(key),
                    Value = new HexBigInteger(BigInteger.Zero),
                    Proof = new List<string>()
                });
            }
        }

        private static StateNotAvailableException MissingNode(byte[] stateRoot, string detail)
            => new StateNotAvailableException(stateRoot,
                $"Missing trie node: a node below state root {(stateRoot == null ? "(null)" : stateRoot.ToHex(true))} is not available on this node{detail}");

        private static byte[] ReadLeaf(PatriciaTrie trie, byte[] hashedKey, byte[] stateRoot)
        {
            try
            {
                return trie.Get(hashedKey);
            }
            catch (MissingTrieNodeException ex)
            {
                throw MissingNode(stateRoot, ": " + ex.Message);
            }
        }

        private static List<string> GenerateProofHex(PatriciaTrie trie, byte[] hashedKey, byte[] stateRoot)
        {
            List<byte[]> proofNodes;
            try
            {
                proofNodes = ProofGenerator.GeneratePathProof(trie, hashedKey);
            }
            catch (MissingTrieNodeException ex)
            {
                throw MissingNode(stateRoot, ": " + ex.Message);
            }
            if (proofNodes == null) throw MissingNode(stateRoot, "");
            return proofNodes.Select(p => p.ToHex(true)).ToList();
        }

        private bool HasWalkableRoot(byte[] root) => _trieNodeStore != null && IsNonEmptyRoot(root);

        private static bool IsNonEmptyRoot(byte[] root)
            => root != null && root.Length == 32 && !root.SequenceEqual(DefaultValues.EMPTY_TRIE_HASH);
    }
}
