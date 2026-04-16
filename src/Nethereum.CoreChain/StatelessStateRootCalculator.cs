using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.CoreChain
{
    public class StatelessStateRootCalculator : IStateRootCalculator
    {
        private static readonly byte[] EMPTY_TRIE_ROOT = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();
        private static readonly byte[] EMPTY_CODE_HASH = Sha3Keccack.Current.CalculateHash(new byte[0]);
        private static readonly Sha3KeccackHashProvider HashProvider = new Sha3KeccackHashProvider();

        private readonly byte[] _preStateRoot;
        private readonly InMemoryTrieStorage _proofStorage;
        private readonly IBlockEncodingProvider _encoding;

        public StatelessStateRootCalculator(byte[] preStateRoot, List<WitnessAccount> accounts, IBlockEncodingProvider encoding)
        {
            _preStateRoot = preStateRoot ?? EMPTY_TRIE_ROOT;
            _proofStorage = new InMemoryTrieStorage();
            _encoding = encoding;

            if (accounts != null)
            {
                foreach (var acc in accounts)
                {
                    LoadProofNodes(acc.AccountProof);
                    if (acc.StorageProofs != null)
                        foreach (var sp in acc.StorageProofs)
                            LoadProofNodes(sp.Proof);
                }
            }
        }

        private void LoadProofNodes(byte[][] proofNodes)
        {
            if (proofNodes == null) return;
            foreach (var node in proofNodes)
            {
                if (node != null && node.Length > 0)
                    _proofStorage.Put(HashProvider.ComputeHash(node), node);
            }
        }

        public byte[] ComputeStateRoot(ExecutionStateService executionState)
        {
            var accounts = executionState.AccountsState;
            if (accounts == null || accounts.Count == 0)
                return _preStateRoot;

            // Create partial trie from pre-state root — proof nodes provide the branches
            var stateTrie = new PatriciaTrie(_preStateRoot);

            foreach (var kvp in accounts)
            {
                var address = kvp.Key;
                var account = kvp.Value;

                if (IsEmptyAccount(account))
                    continue;

                var storageRoot = ComputeStorageRoot(address, account);
                var codeHash = account.Code != null && account.Code.Length > 0
                    ? Sha3Keccack.Current.CalculateHash(account.Code)
                    : EMPTY_CODE_HASH;

                var modelAccount = new Account
                {
                    Nonce = account.Nonce ?? EvmUInt256.Zero,
                    Balance = account.Balance.GetTotalBalance(),
                    StateRoot = storageRoot,
                    CodeHash = codeHash
                };

                var accountRlp = _encoding.EncodeAccount(modelAccount);

                var addressBytes = AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray();
                var addressKey = Sha3Keccack.Current.CalculateHash(addressBytes);

                // Put into partial trie — proof nodes provide sibling hashes
                stateTrie.Put(addressKey, accountRlp, _proofStorage);
            }

            return stateTrie.Root?.GetHash() ?? _preStateRoot;
        }

        private byte[] ComputeStorageRoot(string address, AccountExecutionState account)
        {
            var storage = account.Storage;
            if (storage == null || storage.Count == 0)
                return EMPTY_TRIE_ROOT;

            // Get storage root from the pre-state account in the partial trie
            var storageRoot = GetPreStateStorageRoot(address);

            var storageTrie = new PatriciaTrie(storageRoot);

            foreach (var entry in storage)
            {
                var slotKey = Sha3Keccack.Current.CalculateHash(entry.Key.ToBigEndian());

                if (entry.Value == null || entry.Value.Length == 0 || IsAllZero(entry.Value))
                {
                    // Zero value — effectively delete
                    storageTrie.Delete(slotKey, _proofStorage);
                    continue;
                }

                var valueRlp = RLP.RLP.EncodeElement(TrimLeadingZeros(entry.Value));
                storageTrie.Put(slotKey, valueRlp, _proofStorage);
            }

            return storageTrie.Root?.GetHash() ?? EMPTY_TRIE_ROOT;
        }

        private byte[] GetPreStateStorageRoot(string address)
        {
            var addressBytes = AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray();
            var addressKey = Sha3Keccack.Current.CalculateHash(addressBytes);

            var trie = new PatriciaTrie(_preStateRoot);
            var accountRlp = trie.Get(addressKey, _proofStorage);
            if (accountRlp == null || accountRlp.Length == 0)
                return EMPTY_TRIE_ROOT;

            var decoded = RLP.RLP.Decode(accountRlp);
            if (decoded is RLP.RLPCollection rlp && rlp.Count >= 3)
                return rlp[2].RLPData ?? EMPTY_TRIE_ROOT;

            return EMPTY_TRIE_ROOT;
        }

        private static bool IsEmptyAccount(AccountExecutionState account)
        {
            if (account.Nonce.HasValue && account.Nonce.Value > 0) return false;
            if (!account.Balance.GetTotalBalance().IsZero) return false;
            if (account.Code != null && account.Code.Length > 0) return false;
            if (account.Storage != null)
                foreach (var kvp in account.Storage)
                    if (kvp.Value != null && kvp.Value.Length > 0 && !IsAllZero(kvp.Value))
                        return false;
            return true;
        }

        private static bool IsAllZero(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
                if (bytes[i] != 0) return false;
            return true;
        }

        private static byte[] TrimLeadingZeros(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return new byte[0];
            int start = 0;
            while (start < bytes.Length && bytes[start] == 0) start++;
            if (start == bytes.Length) return new byte[0];
            if (start == 0) return bytes;
            var result = new byte[bytes.Length - start];
            System.Array.Copy(bytes, start, result, 0, result.Length);
            return result;
        }
    }
}
