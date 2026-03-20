using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Ssz;

namespace Nethereum.Model.SSZ
{
    // AccessTuple = Container (standard SSZ)
    //   address: ExecutionAddress (ByteVector[20]) — fixed
    //   storage_keys: ProgressiveList[Hash32] — variable
    public class SszAccessListEncoder
    {
        public const int AddressLength = 20;
        public const int StorageKeyLength = 32;

        public static readonly SszAccessListEncoder Current = new SszAccessListEncoder();

        public byte[] EncodeAccessTuple(AccessListItem item)
        {
            var addressBytes = GetAddressBytes(item.Address);
            var keys = item.StorageKeys ?? new List<byte[]>();

            // Container layout:
            //   Fixed: address(20) + offset_to_storage_keys(4) = 24 bytes fixed section
            //   Variable: storage_keys (each 32 bytes, fixed-size elements in a list)
            const int fixedSize = AddressLength + 4;

            using var writer = new SszWriter();
            writer.WriteFixedBytes(addressBytes, AddressLength);
            writer.WriteUInt32((uint)fixedSize); // offset to variable section
            // ProgressiveList[Hash32]: just write the 32-byte elements sequentially
            foreach (var key in keys)
                writer.WriteFixedBytes(key, StorageKeyLength);

            return writer.ToArray();
        }

        public AccessListItem DecodeAccessTuple(ReadOnlySpan<byte> data)
        {
            var reader = new SszReader(data);
            var address = reader.ReadFixedBytes(AddressLength);
            var storageKeysOffset = reader.ReadUInt32();

            // Remaining bytes are storage keys (each 32 bytes)
            var remaining = data.Length - (int)storageKeysOffset;
            var keyCount = remaining / StorageKeyLength;
            var keys = new List<byte[]>(keyCount);
            for (var i = 0; i < keyCount; i++)
                keys.Add(reader.ReadFixedBytes(StorageKeyLength));

            return new AccessListItem
            {
                Address = "0x" + address.ToHex(),
                StorageKeys = keys
            };
        }

        public byte[] EncodeAccessList(List<AccessListItem> accessList)
        {
            if (accessList == null || accessList.Count == 0)
                return Array.Empty<byte>();

            // SSZ list of variable-size containers: offset table + data
            var encodedItems = new List<byte[]>();
            foreach (var item in accessList)
                encodedItems.Add(EncodeAccessTuple(item));

            // Write offsets (4 bytes each) then concatenated item data
            var offsetsSize = accessList.Count * 4;
            var totalDataSize = 0;
            foreach (var item in encodedItems)
                totalDataSize += item.Length;

            using var writer = new SszWriter();
            // Offsets
            var currentOffset = (uint)offsetsSize;
            foreach (var item in encodedItems)
            {
                writer.WriteUInt32(currentOffset);
                currentOffset += (uint)item.Length;
            }
            // Data
            foreach (var item in encodedItems)
                writer.WriteBytes(item);

            return writer.ToArray();
        }

        public List<AccessListItem> DecodeAccessList(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
                return new List<AccessListItem>();

            // Read offsets to determine item boundaries
            var reader = new SszReader(data);
            var firstOffset = reader.ReadUInt32();
            var itemCount = (int)firstOffset / 4;

            var offsets = new uint[itemCount];
            offsets[0] = firstOffset;
            for (var i = 1; i < itemCount; i++)
                offsets[i] = reader.ReadUInt32();

            var items = new List<AccessListItem>(itemCount);
            for (var i = 0; i < itemCount; i++)
            {
                var start = (int)offsets[i];
                var end = i + 1 < itemCount ? (int)offsets[i + 1] : data.Length;
                items.Add(DecodeAccessTuple(data.Slice(start, end - start)));
            }

            return items;
        }

        public byte[] HashTreeRootAccessTuple(AccessListItem item)
        {
            var addressRoot = SszHashTreeRootHelper.HashTreeRootAddress(item.Address);
            var storageKeysRoot = SszMerkleizer.HashTreeRootProgressiveList(
                item.StorageKeys ?? new List<byte[]>());
            return SszMerkleizer.Merkleize(new List<byte[]> { addressRoot, storageKeysRoot });
        }

        public byte[] HashTreeRootAccessList(List<AccessListItem> accessList)
        {
            var roots = new List<byte[]>();
            if (accessList != null)
                foreach (var item in accessList)
                    roots.Add(HashTreeRootAccessTuple(item));
            return SszMerkleizer.HashTreeRootProgressiveList(roots);
        }

        private static byte[] GetAddressBytes(string address)
        {
            if (string.IsNullOrEmpty(address)) return new byte[AddressLength];
            return address.HexToByteArray();
        }
    }
}
