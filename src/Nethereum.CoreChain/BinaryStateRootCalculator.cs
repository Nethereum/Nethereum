using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Merkle.Binary.Keys;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.CoreChain
{
    public class BinaryStateRootCalculator : IStateRootCalculator
    {
        private static readonly byte[] EMPTY_ROOT = new byte[32];
        private static readonly byte[] EMPTY_CODE_HASH = Sha3Keccack.Current.CalculateHash(new byte[0]);

        private readonly IHashProvider _hashProvider;

        public BinaryStateRootCalculator()
            : this(new Blake3HashProvider())
        {
        }

        public BinaryStateRootCalculator(IHashProvider hashProvider)
        {
            _hashProvider = hashProvider ?? throw new System.ArgumentNullException(nameof(hashProvider));
        }

        public byte[] ComputeStateRoot(ExecutionStateService executionState)
        {
            var accounts = executionState.AccountsState;
            if (accounts == null || accounts.Count == 0)
                return EMPTY_ROOT;

            var trie = new BinaryTrie(_hashProvider);
            var keyDerivation = new BinaryTreeKeyDerivation(_hashProvider);

            foreach (var kvp in accounts)
            {
                var address = kvp.Key;
                var account = kvp.Value;

                if (IsEmptyAccount(account))
                    continue;

                var addressBytes = AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray();

                PutBasicData(trie, keyDerivation, addressBytes, account);
                PutCodeHash(trie, keyDerivation, addressBytes, account);
                PutCodeChunks(trie, keyDerivation, addressBytes, account);
                PutStorage(trie, keyDerivation, addressBytes, account);
            }

            return trie.ComputeRoot();
        }

        private static void PutBasicData(BinaryTrie trie, BinaryTreeKeyDerivation keyDerivation,
            byte[] addressBytes, AccountExecutionState account)
        {
            var nonce = account.Nonce.HasValue ? (ulong)account.Nonce.Value : 0UL;
            var balance = account.Balance.GetTotalBalance();
            var codeSize = (uint)(account.Code != null ? account.Code.Length : 0);

            var leaf = BasicDataLeaf.Pack(0, codeSize, nonce, balance);
            var key = keyDerivation.GetTreeKeyForBasicData(addressBytes);
            trie.Put(key, leaf);
        }

        private static void PutCodeHash(BinaryTrie trie, BinaryTreeKeyDerivation keyDerivation,
            byte[] addressBytes, AccountExecutionState account)
        {
            var codeHash = account.Code != null && account.Code.Length > 0
                ? Sha3Keccack.Current.CalculateHash(account.Code)
                : EMPTY_CODE_HASH;

            var key = keyDerivation.GetTreeKeyForCodeHash(addressBytes);
            trie.Put(key, codeHash);
        }

        private static void PutCodeChunks(BinaryTrie trie, BinaryTreeKeyDerivation keyDerivation,
            byte[] addressBytes, AccountExecutionState account)
        {
            if (account.Code == null || account.Code.Length == 0)
                return;

            var chunks = CodeChunker.ChunkifyCode(account.Code);
            for (int i = 0; i < chunks.Length; i++)
            {
                var key = keyDerivation.GetTreeKeyForCodeChunk(addressBytes, (ulong)i);
                trie.Put(key, chunks[i]);
            }
        }

        private static void PutStorage(BinaryTrie trie, BinaryTreeKeyDerivation keyDerivation,
            byte[] addressBytes, AccountExecutionState account)
        {
            var storage = account.Storage;
            if (storage == null || storage.Count == 0)
                return;

            foreach (var entry in storage)
            {
                if (entry.Value == null || entry.Value.Length == 0 || IsAllZero(entry.Value))
                    continue;

                var value = PadTo32(entry.Value);
                var key = keyDerivation.GetTreeKeyForStorageSlot(addressBytes, entry.Key);
                trie.Put(key, value);
            }
        }

        private static bool IsEmptyAccount(AccountExecutionState account)
        {
            if (account.Nonce.HasValue && account.Nonce.Value > 0) return false;
            if (!account.Balance.GetTotalBalance().IsZero) return false;
            if (account.Code != null && account.Code.Length > 0) return false;
            if (account.Storage != null)
            {
                foreach (var kvp in account.Storage)
                {
                    if (kvp.Value != null && kvp.Value.Length > 0 && !IsAllZero(kvp.Value))
                        return false;
                }
            }
            return true;
        }

        private static bool IsAllZero(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
                if (bytes[i] != 0) return false;
            return true;
        }

        private static byte[] PadTo32(byte[] value)
        {
            if (value.Length == 32) return value;
            if (value.Length > 32) return value;
            var padded = new byte[32];
            System.Array.Copy(value, 0, padded, 32 - value.Length, value.Length);
            return padded;
        }
    }
}
