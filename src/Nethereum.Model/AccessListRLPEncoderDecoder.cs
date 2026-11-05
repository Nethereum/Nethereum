using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;

namespace Nethereum.Model
{
    public class AccessListRLPEncoderDecoder
    {

        public static byte[] EncodeAccessList(List<AccessListItem> accessListItems)
        {
            if (accessListItems == null || accessListItems.Count == 0)   return new[] { RLP.RLP.OFFSET_SHORT_LIST };

            var encodedData = new List<byte[]>();
            
            foreach (var accessListItem in accessListItems)
            {
                var encodedItem = new List<byte[]>();
                encodedItem.Add(RLP.RLP.EncodeElement(accessListItem.Address.HexToByteArray()));
                var encodedStorageKeys = new List<byte[]>();
                foreach (var storageKey in accessListItem.StorageKeys)
                {
                    encodedStorageKeys.Add(RLP.RLP.EncodeElement(storageKey));
                }

                encodedItem.Add(RLP.RLP.EncodeList(encodedStorageKeys.ToArray()));
                encodedData.Add(RLP.RLP.EncodeList(encodedItem.ToArray()));
            }

            return RLP.RLP.EncodeList(encodedData.ToArray());
        }


        public static List<AccessListItem> DecodeAccessList(byte[] accessListEncoded)
        {
            if (accessListEncoded == null || accessListEncoded.Length == 0) return null;

            var reader = new RlpReader(accessListEncoded);
            var accessLists = Read(reader);
            reader.EnsureEnd();
            return accessLists;
        }

        internal static List<AccessListItem> Read(RlpReader reader)
        {
            var scope = reader.ReadListHeader();
            if (reader.IsAtEnd(scope))
            {
                reader.EnsureEnd(scope);
                return null;
            }

            var items = new List<AccessListItem>();
            while (!reader.IsAtEnd(scope))
            {
                var itemScope = reader.ReadListHeader();
                var item = new AccessListItem { Address = reader.ReadFixed(AddressBytes).ToHex() };
                var keysScope = reader.ReadListHeader();
                while (!reader.IsAtEnd(keysScope))
                    item.StorageKeys.Add(reader.ReadFixed(StorageKeyBytes));
                reader.EnsureEnd(keysScope);
                reader.EnsureEnd(itemScope);
                items.Add(item);
            }

            reader.EnsureEnd(scope);
            return items;
        }

        private const int AddressBytes = 20;
        private const int StorageKeyBytes = 32;

    }
}