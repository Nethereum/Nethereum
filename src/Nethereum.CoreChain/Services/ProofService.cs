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

namespace Nethereum.CoreChain.Services
{
    public class ProofService
    {
        private readonly IStateStore _stateStore;
        private readonly ITrieNodeStore _trieNodeStore;
        private readonly RootCalculator _rootCalculator;

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
            var sha3 = new Sha3Keccack();
            var account = await _stateStore.GetAccountAsync(address);

            var addressBytesForProof = address.HexToByteArray();
            if (addressBytesForProof.Length < 20)
            {
                var paddedAddress = new byte[20];
                System.Array.Copy(addressBytesForProof, 0, paddedAddress, 20 - addressBytesForProof.Length, addressBytesForProof.Length);
                addressBytesForProof = paddedAddress;
            }
            var addressHash = sha3.CalculateHash(addressBytesForProof);

            List<string> accountProofHex;

            if (_trieNodeStore != null && stateRoot != null && stateRoot.Length == 32 && !stateRoot.SequenceEqual(DefaultValues.EMPTY_TRIE_HASH))
            {
                var trie = PatriciaTrie.LoadFromStorage(stateRoot, _trieNodeStore);
                var proofStorage = trie.GenerateProof(addressHash, _trieNodeStore);
                accountProofHex = proofStorage?.Storage?.Values?.Where(p => p != null).Select(p => p.ToHex(true)).ToList() ?? new List<string>();
            }
            else
            {
                accountProofHex = await GenerateAccountProofWithFullRebuildAsync(addressHash);
            }

            var accountStorage = await _stateStore.GetAllStorageAsync(address);
            byte[] storageHash = DefaultValues.EMPTY_TRIE_HASH;
            if (accountStorage.Count > 0)
            {
                var storageDict = new Dictionary<byte[], byte[]>();
                foreach (var storageKvp in accountStorage)
                {
                    var slotBytes = storageKvp.Key.ToBytesForRLPEncoding();
                    if (slotBytes.Length < 32)
                    {
                        var paddedSlot = new byte[32];
                        System.Array.Copy(slotBytes, 0, paddedSlot, 32 - slotBytes.Length, slotBytes.Length);
                        slotBytes = paddedSlot;
                    }
                    var hashedSlot = sha3.CalculateHash(slotBytes);
                    storageDict[hashedSlot] = storageKvp.Value;
                }
                storageHash = _rootCalculator.CalculateStorageRoot(storageDict, _trieNodeStore);
            }

            var storageProofs = new List<StorageProof>();
            if (storageKeys != null && storageKeys.Count > 0 && accountStorage != null)
            {
                storageProofs = await GenerateStorageProofsAsync(address, storageKeys, accountStorage);
            }
            else if (storageKeys != null && storageKeys.Count > 0)
            {
                foreach (var key in storageKeys)
                {
                    storageProofs.Add(new StorageProof
                    {
                        Key = new HexBigInteger(key),
                        Value = new HexBigInteger(BigInteger.Zero),
                        Proof = new List<string>()
                    });
                }
            }

            return new AccountProof
            {
                Address = address,
                Balance = new HexBigInteger(account?.Balance ?? BigInteger.Zero),
                CodeHash = (account?.CodeHash ?? DefaultValues.EMPTY_DATA_HASH).ToHex(true),
                Nonce = new HexBigInteger(account?.Nonce ?? BigInteger.Zero),
                StorageHash = storageHash.ToHex(true),
                AccountProofs = accountProofHex,
                StorageProof = storageProofs
            };
        }

        private async Task<List<string>> GenerateAccountProofWithFullRebuildAsync(byte[] addressHash)
        {
            var sha3 = new Sha3Keccack();
            var accounts = await _stateStore.GetAllAccountsAsync();

            var trie = new PatriciaTrie();
            ITrieNodeStore nodeStore = _trieNodeStore ?? new InMemoryTrieNodeStore();

            foreach (var kvp in accounts)
            {
                if (kvp.Value == null) continue;

                var addressBytes = kvp.Key.HexToByteArray();
                if (addressBytes.Length < 20)
                {
                    var paddedAddress = new byte[20];
                    System.Array.Copy(addressBytes, 0, paddedAddress, 20 - addressBytes.Length, addressBytes.Length);
                    addressBytes = paddedAddress;
                }
                var hashedKey = sha3.CalculateHash(addressBytes);

                var storage = await _stateStore.GetAllStorageAsync(kvp.Key);
                var acc = new Account
                {
                    Nonce = kvp.Value.Nonce,
                    Balance = kvp.Value.Balance,
                    CodeHash = kvp.Value.CodeHash ?? DefaultValues.EMPTY_DATA_HASH
                };

                if (storage.Count > 0)
                {
                    var storageDict = new Dictionary<byte[], byte[]>();
                    foreach (var storageKvp in storage)
                    {
                        var slotBytes = storageKvp.Key.ToBytesForRLPEncoding();
                        if (slotBytes.Length < 32)
                        {
                            var paddedSlot = new byte[32];
                            System.Array.Copy(slotBytes, 0, paddedSlot, 32 - slotBytes.Length, slotBytes.Length);
                            slotBytes = paddedSlot;
                        }
                        var hashedSlot = sha3.CalculateHash(slotBytes);
                        storageDict[hashedSlot] = storageKvp.Value;
                    }
                    acc.StateRoot = _rootCalculator.CalculateStorageRoot(storageDict, _trieNodeStore);
                }
                else
                {
                    acc.StateRoot = DefaultValues.EMPTY_TRIE_HASH;
                }

                var encodedAccount = AccountEncoder.Current.Encode(acc);
                trie.Put(hashedKey, encodedAccount, nodeStore);
            }

            var proofStorage = trie.GenerateProof(addressHash);
            return proofStorage?.Storage?.Values?.Where(p => p != null).Select(p => p.ToHex(true)).ToList() ?? new List<string>();
        }

        private async Task<List<StorageProof>> GenerateStorageProofsAsync(
            string address,
            List<BigInteger> storageKeys,
            Dictionary<BigInteger, byte[]> storage)
        {
            var sha3 = new Sha3Keccack();
            var proofs = new List<StorageProof>();

            if (storage.Count == 0)
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
                return proofs;
            }

            var storageTrie = new PatriciaTrie();
            var storageNodeStore = new InMemoryTrieNodeStore();

            foreach (var kvp in storage)
            {
                if (kvp.Value == null) continue;

                var slotBytes = kvp.Key.ToBytesForRLPEncoding();
                if (slotBytes.Length < 32)
                {
                    var paddedSlot = new byte[32];
                    System.Array.Copy(slotBytes, 0, paddedSlot, 32 - slotBytes.Length, slotBytes.Length);
                    slotBytes = paddedSlot;
                }
                var hashedSlot = sha3.CalculateHash(slotBytes);
                var encodedValue = RLP.RLP.EncodeElement(kvp.Value);
                storageTrie.Put(hashedSlot, encodedValue, storageNodeStore);
            }

            foreach (var key in storageKeys)
            {
                var slotBytes = key.ToBytesForRLPEncoding();
                if (slotBytes.Length < 32)
                {
                    var paddedSlot = new byte[32];
                    System.Array.Copy(slotBytes, 0, paddedSlot, 32 - slotBytes.Length, slotBytes.Length);
                    slotBytes = paddedSlot;
                }
                var hashedSlot = sha3.CalculateHash(slotBytes);

                var proof = storageTrie.GenerateProof(hashedSlot);
                var proofHex = proof?.Storage?.Values?.Where(p => p != null).Select(p => p.ToHex(true)).ToList() ?? new List<string>();

                storage.TryGetValue(key, out var value);
                var valueBigInt = value != null ? value.ToBigIntegerFromRLPDecoded() : BigInteger.Zero;

                proofs.Add(new StorageProof
                {
                    Key = new HexBigInteger(key),
                    Value = new HexBigInteger(valueBigInt),
                    Proof = proofHex
                });
            }

            return proofs;
        }
    }
}
