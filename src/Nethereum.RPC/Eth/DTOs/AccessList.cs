using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Nethereum.RPC.Eth.DTOs
{
    public class AccessList
    {
      [DataMember(Name = "address")]
        public string Address { get; set; }
      [DataMember(Name = "storageKeys")]
        public List<string> StorageKeys { get; set; }
    }
}