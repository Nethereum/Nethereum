using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.CoreChain
{
    public class StateRootCalculator
    {
        private readonly IHashProvider _hashProvider;
        private readonly Sha3Keccack _sha3;

        public StateRootCalculator() : this(new Sha3KeccackHashProvider())
        {
        }

        public StateRootCalculator(IHashProvider hashProvider)
        {
            _hashProvider = hashProvider;
            _sha3 = new Sha3Keccack();
        }

        public async Task<byte[]> ComputeStateRootAsync(IStateStore stateStore, ITrieNodeStore trieNodeStore = null)
        {
            var accounts = await stateStore.GetAllAccountsAsync();
            if (accounts.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var stateTrie = new PatriciaTrie(_hashProvider);

            foreach (var kvp in accounts)
            {
                var address = kvp.Key;
                var account = kvp.Value;

                var hashedKey = GetHashedAddressKey(address);

                var storage = await stateStore.GetAllStorageAsync(address);
                var accountForTrie = new Account
                {
                    Nonce = account.Nonce,
                    Balance = account.Balance,
                    CodeHash = account.CodeHash ?? DefaultValues.EMPTY_DATA_HASH
                };

                var filteredStorage = storage.Where(kvp =>
                    kvp.Value != null &&
                    kvp.Value.Length > 0 &&
                    !kvp.Value.All(b => b == 0)).ToList();

                if (filteredStorage.Count > 0)
                {
                    var storageTrie = new PatriciaTrie(_hashProvider);
                    foreach (var storageKvp in filteredStorage)
                    {
                        var slot = storageKvp.Key;
                        var value = storageKvp.Value;

                        var hashedSlot = GetHashedSlotKey(slot);
                        var trimmedValue = TrimLeadingZeros(value);
                        var encodedValue = RLP.RLP.EncodeElement(trimmedValue);
                        storageTrie.Put(hashedSlot, encodedValue, trieNodeStore);
                    }

                    if (trieNodeStore != null)
                    {
                        storageTrie.SaveNodesToStorage(trieNodeStore);
                    }

                    accountForTrie.StateRoot = storageTrie.Root.GetHash();
                }
                else
                {
                    accountForTrie.StateRoot = DefaultValues.EMPTY_TRIE_HASH;
                }

                var encodedAccount = AccountEncoder.Current.Encode(accountForTrie);
                stateTrie.Put(hashedKey, encodedAccount, trieNodeStore);
            }

            if (stateTrie.Root is EmptyNode)
                return DefaultValues.EMPTY_TRIE_HASH;

            if (trieNodeStore != null)
            {
                stateTrie.SaveNodesToStorage(trieNodeStore);
            }

            return stateTrie.Root.GetHash();
        }

        public byte[] ComputeStateRoot(IDictionary<string, Account> accounts, IDictionary<string, IDictionary<BigInteger, byte[]>> storageByAddress = null, ITrieNodeStore trieNodeStore = null)
        {
            if (accounts == null || accounts.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var stateTrie = new PatriciaTrie(_hashProvider);

            foreach (var kvp in accounts)
            {
                var address = kvp.Key;
                var account = kvp.Value;

                var hashedKey = GetHashedAddressKey(address);

                var accountForTrie = new Account
                {
                    Nonce = account.Nonce,
                    Balance = account.Balance,
                    CodeHash = account.CodeHash ?? DefaultValues.EMPTY_DATA_HASH
                };

                IDictionary<BigInteger, byte[]> storage = null;
                storageByAddress?.TryGetValue(address, out storage);

                var filteredStorage = storage?.Where(kvp =>
                    kvp.Value != null &&
                    kvp.Value.Length > 0 &&
                    !kvp.Value.All(b => b == 0)).ToList();

                if (filteredStorage != null && filteredStorage.Count > 0)
                {
                    var storageTrie = new PatriciaTrie(_hashProvider);
                    foreach (var storageKvp in filteredStorage)
                    {
                        var slot = storageKvp.Key;
                        var value = storageKvp.Value;

                        var hashedSlot = GetHashedSlotKey(slot);
                        var trimmedValue = TrimLeadingZeros(value);
                        var encodedValue = RLP.RLP.EncodeElement(trimmedValue);
                        storageTrie.Put(hashedSlot, encodedValue, trieNodeStore);
                    }

                    if (trieNodeStore != null)
                    {
                        storageTrie.SaveNodesToStorage(trieNodeStore);
                    }

                    accountForTrie.StateRoot = storageTrie.Root.GetHash();
                }
                else
                {
                    accountForTrie.StateRoot = DefaultValues.EMPTY_TRIE_HASH;
                }

                var encodedAccount = AccountEncoder.Current.Encode(accountForTrie);
                stateTrie.Put(hashedKey, encodedAccount, trieNodeStore);
            }

            if (stateTrie.Root is EmptyNode)
                return DefaultValues.EMPTY_TRIE_HASH;

            if (trieNodeStore != null)
            {
                stateTrie.SaveNodesToStorage(trieNodeStore);
            }

            return stateTrie.Root.GetHash();
        }

        private byte[] GetHashedAddressKey(string address)
        {
            var addressBytes = AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray();
            return _sha3.CalculateHash(addressBytes);
        }

        private byte[] GetHashedSlotKey(BigInteger slot)
        {
            var slotBytes = slot.ToBytesForRLPEncoding().PadBytes(32);
            return _sha3.CalculateHash(slotBytes);
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
