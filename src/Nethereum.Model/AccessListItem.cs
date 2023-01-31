using System.Collections.Generic;

namespace Nethereum.Model
{

    public class AccessListItem
    {
        public string Address { get; set; }
        public List<byte[]> StorageKeys { get; set; }

        public AccessListItem()
        {
            StorageKeys = new List<byte[]>();
        }

        public AccessListItem(string address, List<byte[]> storageKeys)
        {
            this.Address = address;
            this.StorageKeys = storageKeys;
        }
    }
}