using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.RLP;
using Nethereum.Util;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class StateRootCalculator
    {
        private static readonly byte[] EMPTY_TRIE_ROOT = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();
        private static readonly byte[] EMPTY_CODE_HASH = Sha3Keccack.Current.CalculateHash(new byte[0]);

        public static byte[] CalculateStateRoot(Dictionary<string, AccountState> accounts)
        {
            if (accounts == null || accounts.Count == 0)
                return EMPTY_TRIE_ROOT;

            var stateTrie = new PatriciaTrie();

            foreach (var accountEntry in accounts)
            {
                var addressHex = accountEntry.Key.EnsureHexPrefix().ToLowerInvariant();
                var account = accountEntry.Value;

                if (IsEmptyAccount(account))
                    continue;

                var storageRoot = CalculateStorageRoot(account.Storage);
                var codeHash = account.Code != null && account.Code.Length > 0
                    ? Sha3Keccack.Current.CalculateHash(account.Code)
                    : EMPTY_CODE_HASH;

                var accountRlp = EncodeAccount(account.Nonce, account.Balance, storageRoot, codeHash);
                var addressKey = Sha3Keccack.Current.CalculateHash(addressHex.HexToByteArray());

                stateTrie.Put(addressKey, accountRlp);
            }

            return stateTrie.Root?.GetHash() ?? EMPTY_TRIE_ROOT;
        }

        private static bool IsEmptyAccount(AccountState account)
        {
            if (account.Nonce != 0) return false;
            if (account.Balance != 0) return false;
            if (account.Code != null && account.Code.Length > 0) return false;
            if (account.Storage != null && account.Storage.Any(kvp =>
                kvp.Value != null && kvp.Value.Length > 0 && !kvp.Value.All(b => b == 0)))
                return false;
            return true;
        }

        public static byte[] CalculateStorageRoot(Dictionary<BigInteger, byte[]> storage)
        {
            if (storage == null || storage.Count == 0)
                return EMPTY_TRIE_ROOT;

            var filteredStorage = storage.Where(kvp =>
                kvp.Value != null &&
                kvp.Value.Length > 0 &&
                !kvp.Value.All(b => b == 0)).ToList();

            if (filteredStorage.Count == 0)
                return EMPTY_TRIE_ROOT;

            var storageTrie = new PatriciaTrie();

            foreach (var storageEntry in filteredStorage)
            {
                var slotKey = Sha3Keccack.Current.CalculateHash(storageEntry.Key.ToBytesForRLPEncoding().PadTo32Bytes());
                var valueRlp = RLP.RLP.EncodeElement(TrimLeadingZeros(storageEntry.Value));
                storageTrie.Put(slotKey, valueRlp);
            }

            return storageTrie.Root?.GetHash() ?? EMPTY_TRIE_ROOT;
        }

        private static byte[] EncodeAccount(BigInteger nonce, BigInteger balance, byte[] storageRoot, byte[] codeHash)
        {
            var nonceBytes = nonce == 0 ? new byte[0] : TrimLeadingZeros(nonce.ToByteArray().Reverse().ToArray());
            var balanceBytes = balance == 0 ? new byte[0] : TrimLeadingZeros(balance.ToByteArray().Reverse().ToArray());

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(nonceBytes),
                RLP.RLP.EncodeElement(balanceBytes),
                RLP.RLP.EncodeElement(storageRoot),
                RLP.RLP.EncodeElement(codeHash)
            );
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

    public class AccountState
    {
        public BigInteger Nonce { get; set; }
        public BigInteger Balance { get; set; }
        public byte[] Code { get; set; }
        public Dictionary<BigInteger, byte[]> Storage { get; set; }

        public AccountState()
        {
            Storage = new Dictionary<BigInteger, byte[]>();
            Code = new byte[0];
        }
    }
}
