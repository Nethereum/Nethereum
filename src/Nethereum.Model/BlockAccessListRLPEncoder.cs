using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model
{
    public class BlockAccessListRLPEncoder
    {
        private const int AddressLength = 20;

        public static BlockAccessListRLPEncoder Current { get; } = new BlockAccessListRLPEncoder();

        public byte[] Encode(List<AccountChanges> blockAccessList)
        {
            if (blockAccessList == null) throw new System.ArgumentNullException(nameof(blockAccessList));

            var accounts = new byte[blockAccessList.Count][];
            for (var i = 0; i < blockAccessList.Count; i++)
                accounts[i] = EncodeAccount(blockAccessList[i]);

            return RLP.RLP.EncodeList(accounts);
        }

        public byte[] Hash(List<AccountChanges> blockAccessList)
            => new Sha3Keccack().CalculateHash(Encode(blockAccessList));

        public List<AccountChanges> Decode(byte[] data)
        {
            if (data == null) throw new System.ArgumentNullException(nameof(data));

            var accounts = (RLPCollection)RLP.RLP.Decode(data);
            var result = new List<AccountChanges>(accounts.Count);
            foreach (RLPCollection accountRlp in accounts)
                result.Add(DecodeAccount(accountRlp));
            return result;
        }

        private static AccountChanges DecodeAccount(RLPCollection accountRlp)
        {
            return new AccountChanges(accountRlp[0].RLPData.ToHex(true))
            {
                StorageChanges = DecodeStorageChanges((RLPCollection)accountRlp[1]),
                StorageReads = DecodeStorageReads((RLPCollection)accountRlp[2]),
                BalanceChanges = DecodeIndexed((RLPCollection)accountRlp[3],
                    (index, value) => new BalanceChange(index, value.ToEvmUInt256FromRLPDecoded())),
                NonceChanges = DecodeIndexed((RLPCollection)accountRlp[4],
                    (index, value) => new NonceChange(index, value.ToULongFromRLPDecoded())),
                CodeChanges = DecodeIndexed((RLPCollection)accountRlp[5],
                    (index, value) => new CodeChange(index, value ?? RLP.RLP.EMPTY_BYTE_ARRAY))
            };
        }

        private static List<SlotChanges> DecodeStorageChanges(RLPCollection storageChangesRlp)
        {
            var result = new List<SlotChanges>(storageChangesRlp.Count);
            foreach (RLPCollection slotRlp in storageChangesRlp)
            {
                result.Add(new SlotChanges(slotRlp[0].RLPData.ToEvmUInt256FromRLPDecoded())
                {
                    Changes = DecodeIndexed((RLPCollection)slotRlp[1],
                        (index, value) => new StorageChange(index, value.ToEvmUInt256FromRLPDecoded()))
                });
            }
            return result;
        }

        private static List<EvmUInt256> DecodeStorageReads(RLPCollection storageReadsRlp)
        {
            var result = new List<EvmUInt256>(storageReadsRlp.Count);
            foreach (var slotRlp in storageReadsRlp)
                result.Add(slotRlp.RLPData.ToEvmUInt256FromRLPDecoded());
            return result;
        }

        private static List<T> DecodeIndexed<T>(RLPCollection changesRlp, System.Func<ulong, byte[], T> factory)
        {
            var result = new List<T>(changesRlp.Count);
            foreach (RLPCollection changeRlp in changesRlp)
                result.Add(factory(changeRlp[0].RLPData.ToULongFromRLPDecoded(), changeRlp[1].RLPData));
            return result;
        }

        private static byte[] EncodeAccount(AccountChanges account)
        {
            var address = account.Address.HexToByteArray();
            if (address.Length != AddressLength)
                throw new System.ArgumentException(
                    $"Account address must be {AddressLength} bytes; got {address.Length} for '{account.Address}'.",
                    nameof(account));

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(address),
                EncodeStorageChanges(account.StorageChanges),
                EncodeStorageReads(account.StorageReads),
                EncodeIndexed(account.BalanceChanges, c => c.BlockAccessIndex, c => Number(c.PostBalance)),
                EncodeIndexed(account.NonceChanges, c => c.BlockAccessIndex, c => Number(c.NewNonce)),
                EncodeIndexed(account.CodeChanges, c => c.BlockAccessIndex, c => c.NewCode ?? RLP.RLP.EMPTY_BYTE_ARRAY));
        }

        private static byte[] EncodeStorageChanges(List<SlotChanges> storageChanges)
        {
            if (storageChanges == null) return RLP.RLP.EncodeList();

            var slots = new byte[storageChanges.Count][];
            for (var i = 0; i < storageChanges.Count; i++)
            {
                var slot = storageChanges[i];
                slots[i] = RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(Number(slot.Slot)),
                    EncodeIndexed(slot.Changes, c => c.BlockAccessIndex, c => Number(c.PostValue)));
            }

            return RLP.RLP.EncodeList(slots);
        }

        private static byte[] EncodeStorageReads(List<EvmUInt256> storageReads)
        {
            if (storageReads == null) return RLP.RLP.EncodeList();

            var slots = new byte[storageReads.Count][];
            for (var i = 0; i < storageReads.Count; i++)
                slots[i] = RLP.RLP.EncodeElement(Number(storageReads[i]));

            return RLP.RLP.EncodeList(slots);
        }

        private static byte[] EncodeIndexed<T>(
            List<T> changes,
            System.Func<T, ulong> index,
            System.Func<T, byte[]> value)
        {
            if (changes == null) return RLP.RLP.EncodeList();

            var encoded = new byte[changes.Count][];
            for (var i = 0; i < changes.Count; i++)
                encoded[i] = RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(Number(index(changes[i]))),
                    RLP.RLP.EncodeElement(value(changes[i])));

            return RLP.RLP.EncodeList(encoded);
        }

        private static byte[] Number(EvmUInt256 value) => value.ToBytesForRLPEncoding();

        private static byte[] Number(ulong value) => new EvmUInt256(value).ToBytesForRLPEncoding();
    }
}
