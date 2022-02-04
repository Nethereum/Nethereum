using System.Runtime.Serialization;

namespace Nethereum.Quorum.RPC.DTOs
{
    [DataContract]
    public class PrivateRawTransaction
    {
        public PrivateRawTransaction()
        {
        }

        public PrivateRawTransaction(string[] privateFor)
        {
            PrivateFor = privateFor;
        }

        [DataMember(Name =  "privateFor")]
        public string[] PrivateFor { get; set; }
    }
}