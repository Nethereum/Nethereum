using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using System.Collections.Generic;

namespace Nethereum.RPC.Eth.Mappers
{

    public static class AccessListRPCMapper
    {
        public static List<AccessListItem> ToSignerAccessListItemArray(this List<AccessList> accessLists)
        {
            if (accessLists == null) return null;
            var accessListsReturn = new List<AccessListItem>();
            foreach (var sourceAccessListItem in accessLists)
            {
                var accessListItem = new AccessListItem();
                accessListItem.Address = sourceAccessListItem.Address;
                accessListItem.StorageKeys = new List<byte[]>();
                foreach (var storageKey in sourceAccessListItem.StorageKeys)
                {
                    accessListItem.StorageKeys.Add(storageKey.HexToByteArray());
                }
                accessListsReturn.Add(accessListItem);
            }

            return accessListsReturn;
        }

        public static List<AccessList> ToRPCAccessList(this List<AccessListItem> accessList)
        {
            var result = new List<AccessList>();
            if (accessList == null) return result;

            foreach (var item in accessList)
            {
                result.Add(new AccessList
                {
                    Address = item.Address.ConvertToValid20ByteAddressLowerCase(),
                    StorageKeys = item.StorageKeys?.ConvertAll(key => key.ToHex(true)) ?? new List<string>()
                });
            }

            return result;
        }
    }
}
