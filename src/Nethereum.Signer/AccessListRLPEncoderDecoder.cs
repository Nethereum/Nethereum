using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;

namespace Nethereum.Signer
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
            if (accessListEncoded == null || accessListEncoded.Length == 0 || accessListEncoded[0] ==  RLP.RLP.OFFSET_SHORT_LIST ) return null;

            var decodedList = (RLPCollection)(RLP.RLP.Decode(accessListEncoded));

            var accessLists = new List<AccessListItem>();
            foreach (var rlpElement in decodedList)
            {
                var decodedItem =  (RLPCollection)rlpElement;
                var accessListItem = new AccessListItem();
                accessListItem.Address = decodedItem[0].RLPData.ToHex();
                var decodedStorageKeys = (RLPCollection)decodedItem[1];

                foreach (var decodedStorageKey in decodedStorageKeys)
                {
                    accessListItem.StorageKeys.Add(decodedStorageKey.RLPData);   
                }
                accessLists.Add(accessListItem);
            }

            return accessLists;
        }

    }
}