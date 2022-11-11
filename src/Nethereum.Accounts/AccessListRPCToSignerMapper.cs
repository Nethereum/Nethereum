using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;

namespace Nethereum.Web3.Accounts
{
    public static class AccessListRPCToSignerMapper
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