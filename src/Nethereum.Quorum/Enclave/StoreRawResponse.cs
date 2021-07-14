using System.Runtime.Serialization;

namespace Nethereum.Quorum.Enclave
{
    public class StoreRawResponse
    {
        [DataMember(Name =  "key")]
        public string Key { get; set; }
    }
}