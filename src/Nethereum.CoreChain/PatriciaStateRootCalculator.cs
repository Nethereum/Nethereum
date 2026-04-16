using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public class PatriciaStateRootCalculator : IStateRootCalculator
    {
        private static readonly byte[] EMPTY_TRIE_ROOT = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();
        private static readonly byte[] EMPTY_CODE_HASH = Sha3Keccack.Current.CalculateHash(new byte[0]);

        private readonly IBlockEncodingProvider _encoding;

        public PatriciaStateRootCalculator(IBlockEncodingProvider encoding)
        {
            _encoding = encoding;
        }

        public byte[] ComputeStateRoot(ExecutionStateService executionState)
        {
            var accounts = executionState.AccountsState;
            if (accounts == null || accounts.Count == 0)
                return EMPTY_TRIE_ROOT;

            var stateTrie = new PatriciaTrie();

            foreach (var kvp in accounts)
            {
                var address = kvp.Key;
                var account = kvp.Value;

                if (IsEmptyAccount(account))
                    continue;

                var storageRoot = ComputeStorageRoot(account);
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

                stateTrie.Put(addressKey, accountRlp);
            }

            return stateTrie.Root?.GetHash() ?? EMPTY_TRIE_ROOT;
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

        private static byte[] ComputeStorageRoot(AccountExecutionState account)
        {
            var storage = account.Storage;
            if (storage == null || storage.Count == 0)
                return EMPTY_TRIE_ROOT;

            var storageTrie = new PatriciaTrie();
            bool hasNonZero = false;

            foreach (var entry in storage)
            {
                if (entry.Value == null || entry.Value.Length == 0 || IsAllZero(entry.Value))
                    continue;

                hasNonZero = true;
                var slotBytes = entry.Key.ToBigEndian();
                var slotKey = Sha3Keccack.Current.CalculateHash(slotBytes);
                var valueRlp = RLP.RLP.EncodeElement(TrimLeadingZeros(entry.Value));
                storageTrie.Put(slotKey, valueRlp);
            }

            if (!hasNonZero)
                return EMPTY_TRIE_ROOT;

            return storageTrie.Root?.GetHash() ?? EMPTY_TRIE_ROOT;
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

            if (firstNonZero == 0)
                return bytes;

            var result = new byte[bytes.Length - firstNonZero];
            System.Array.Copy(bytes, firstNonZero, result, 0, result.Length);
            return result;
        }
    }
}
