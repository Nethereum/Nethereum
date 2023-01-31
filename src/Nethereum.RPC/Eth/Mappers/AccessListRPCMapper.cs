using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

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
    }
}
