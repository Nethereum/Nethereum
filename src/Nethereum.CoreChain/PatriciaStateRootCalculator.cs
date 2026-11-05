using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public class PatriciaStateRootCalculator : IStateRootCalculator
    {
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
                return DefaultValues.EMPTY_TRIE_HASH;

            var stateTrie = new PatriciaTrie();

            foreach (var kvp in accounts)
            {
                var account = kvp.Value;

                if (StateRootAccountRules.IsEmptyAccount(account))
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
                var addressBytes = kvp.Key.ToByteArray();
                var addressKey = Sha3Keccack.Current.CalculateHash(addressBytes);

                stateTrie.Put(addressKey, accountRlp);
            }

            return stateTrie.Root?.GetHash() ?? DefaultValues.EMPTY_TRIE_HASH;
        }

        private static byte[] ComputeStorageRoot(AccountExecutionState account)
        {
            var storage = account.Storage;
            if (storage == null || storage.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var storageTrie = new PatriciaTrie();
            bool hasNonZero = false;

            foreach (var entry in storage)
            {
                if (entry.Value == null || entry.Value.Length == 0 || ByteUtil.IsZero(entry.Value))
                    continue;

                hasNonZero = true;
                var slotKey = Sha3Keccack.Current.CalculateHash(entry.Key.ToBigEndian());
                var valueRlp = RLP.RLP.EncodeElement(StateRootAccountRules.TrimLeadingZeros(entry.Value));
                storageTrie.Put(slotKey, valueRlp);
            }

            if (!hasNonZero)
                return DefaultValues.EMPTY_TRIE_HASH;

            return storageTrie.Root?.GetHash() ?? DefaultValues.EMPTY_TRIE_HASH;
        }
    }
}
