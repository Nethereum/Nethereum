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

            var reader = new RlpReader(data);
            var accounts = reader.ReadListHeader();
            var result = new List<AccountChanges>();
            while (!reader.IsAtEnd(accounts))
                result.Add(ReadAccount(reader));
            reader.EnsureEnd(accounts);
            reader.EnsureEnd();
            return result;
        }

        private static AccountChanges ReadAccount(RlpReader reader)
        {
            var scope = reader.ReadListHeader();
            var account = new AccountChanges(reader.ReadFixed(AddressLength).ToHex(true))
            {
                StorageChanges = ReadStorageChanges(reader),
                StorageReads = ReadStorageReads(reader),
                BalanceChanges = ReadIndexed(reader, ReadUInt256, (index, value) => new BalanceChange(index, value)),
                NonceChanges = ReadIndexed(reader, r => r.ReadUInt64(), (index, value) => new NonceChange(index, value)),
                CodeChanges = ReadIndexed(reader, r => r.ReadBytes(), (index, value) => new CodeChange(index, value))
            };
            reader.EnsureEnd(scope);
            return account;
        }

        private static List<SlotChanges> ReadStorageChanges(RlpReader reader)
        {
            var scope = reader.ReadListHeader();
            var result = new List<SlotChanges>();
            while (!reader.IsAtEnd(scope))
            {
                var slotScope = reader.ReadListHeader();
                var slot = new SlotChanges(ReadUInt256(reader));
                slot.Changes = ReadIndexed(reader, ReadUInt256, (index, value) => new StorageChange(index, value));
                reader.EnsureEnd(slotScope);
                result.Add(slot);
            }
            reader.EnsureEnd(scope);
            return result;
        }

        private static List<EvmUInt256> ReadStorageReads(RlpReader reader)
        {
            var scope = reader.ReadListHeader();
            var result = new List<EvmUInt256>();
            while (!reader.IsAtEnd(scope))
                result.Add(ReadUInt256(reader));
            reader.EnsureEnd(scope);
            return result;
        }

        private static List<T> ReadIndexed<TValue, T>(RlpReader reader, System.Func<RlpReader, TValue> readValue,
            System.Func<ulong, TValue, T> factory)
        {
            var scope = reader.ReadListHeader();
            var result = new List<T>();
            while (!reader.IsAtEnd(scope))
            {
                var changeScope = reader.ReadListHeader();
                var index = reader.ReadUInt64();
                if (index > uint.MaxValue)
                    throw new System.InvalidOperationException("RLP block access index does not fit in 32 bits");
                var value = readValue(reader);
                reader.EnsureEnd(changeScope);
                result.Add(factory(index, value));
            }
            reader.EnsureEnd(scope);
            return result;
        }

        private static EvmUInt256 ReadUInt256(RlpReader reader)
        {
            var value = reader.ReadBytes();
            if (!RlpScalar.FitsA256BitField(value))
                throw new System.InvalidOperationException("RLP integer does not fit in 256 bits");
            if (!RlpScalar.IsCanonical(value))
                throw new System.InvalidOperationException("RLP integer has a leading zero");
            return value.ToEvmUInt256FromRLPDecoded();
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
