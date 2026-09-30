using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Rpc
{
    /// <summary>
    /// Projects a decoded EIP-7928 block access list onto the JSON shape
    /// <c>eth_getBlockAccessList</c> answers with (execution-apis
    /// <c>src/schemas/block-access-list.yaml</c>).
    ///
    /// <para>Two hex conventions meet here and the schema separates them: a slot key, a
    /// slot read and a post-value are <c>hash32</c> and go out full width; a balance, a
    /// nonce and a block access index are quantities and go out minimally. The two agree
    /// only on zero, so reading one off the wrong side is invisible in the case that is
    /// easiest to test.</para>
    ///
    /// <para>The order is the order the list arrived in. EIP-7928: <i>"Accounts:
    /// Lexicographic by address"</i> — the RLP the store holds is what the header's hash
    /// commits to, so re-sorting here would answer with an order the commitment does not
    /// cover.</para>
    /// </summary>
    public static class BlockAccessListRpcMapper
    {
        public static List<AccountAccessDto> ToDto(List<AccountChanges> blockAccessList)
        {
            var accounts = new List<AccountAccessDto>();
            if (blockAccessList == null) return accounts;

            foreach (var account in blockAccessList)
                accounts.Add(ToDto(account));
            return accounts;
        }

        private static AccountAccessDto ToDto(AccountChanges account) => new AccountAccessDto
        {
            Address = account.Address,
            StorageChanges = ToSlotChanges(account.StorageChanges),
            StorageReads = ToStorageReads(account.StorageReads),
            BalanceChanges = ToBalanceChanges(account.BalanceChanges),
            NonceChanges = ToNonceChanges(account.NonceChanges),
            CodeChanges = ToCodeChanges(account.CodeChanges)
        };

        private static List<SlotChangesDto> ToSlotChanges(List<SlotChanges> slots)
        {
            var dtos = new List<SlotChangesDto>();
            if (slots == null) return dtos;

            foreach (var slot in slots)
                dtos.Add(new SlotChangesDto
                {
                    Key = Hash32(slot.Slot),
                    Changes = ToStorageChanges(slot.Changes)
                });
            return dtos;
        }

        private static List<StorageChangeDto> ToStorageChanges(List<StorageChange> changes)
        {
            var dtos = new List<StorageChangeDto>();
            if (changes == null) return dtos;

            foreach (var change in changes)
                dtos.Add(new StorageChangeDto
                {
                    Index = Quantity(change.BlockAccessIndex),
                    Value = Hash32(change.PostValue)
                });
            return dtos;
        }

        private static List<string> ToStorageReads(List<EvmUInt256> reads)
        {
            var dtos = new List<string>();
            if (reads == null) return dtos;

            foreach (var read in reads)
                dtos.Add(Hash32(read));
            return dtos;
        }

        private static List<BalanceChangeDto> ToBalanceChanges(List<BalanceChange> changes)
        {
            var dtos = new List<BalanceChangeDto>();
            if (changes == null) return dtos;

            foreach (var change in changes)
                dtos.Add(new BalanceChangeDto
                {
                    Index = Quantity(change.BlockAccessIndex),
                    Value = Quantity(change.PostBalance)
                });
            return dtos;
        }

        private static List<NonceChangeDto> ToNonceChanges(List<NonceChange> changes)
        {
            var dtos = new List<NonceChangeDto>();
            if (changes == null) return dtos;

            foreach (var change in changes)
                dtos.Add(new NonceChangeDto
                {
                    Index = Quantity(change.BlockAccessIndex),
                    Value = Quantity(change.NewNonce)
                });
            return dtos;
        }

        private static List<CodeChangeDto> ToCodeChanges(List<CodeChange> changes)
        {
            var dtos = new List<CodeChangeDto>();
            if (changes == null) return dtos;

            foreach (var change in changes)
                dtos.Add(new CodeChangeDto
                {
                    Index = Quantity(change.BlockAccessIndex),
                    Code = (change.NewCode ?? new byte[0]).ToHex(true)
                });
            return dtos;
        }

        private static string Hash32(EvmUInt256 value) => value.ToBigEndian().ToHex(true);

        private static string Quantity(ulong value) => ((BigInteger)value).ToHex(false);

        private static string Quantity(EvmUInt256 value) => ((BigInteger)value).ToHex(false);
    }
}
