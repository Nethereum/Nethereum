using System.Runtime.Serialization;

namespace Nethereum.Quorum.Enclave
{
    [DataContract]
    public class StoreRawResponse
    {
        [DataMember(Name =  "key")]
        public string Key { get; set; }
    }
}