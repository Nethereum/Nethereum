using Newtonsoft.Json;

namespace Nethereum.Quorum.RPC.DTOs
{
    public class PrivateRawTransaction
    {
        public PrivateRawTransaction()
        {
        }

        public PrivateRawTransaction(string[] privateFor)
        {
            PrivateFor = privateFor;
        }

        [JsonProperty(PropertyName = "privateFor")]
        public string[] PrivateFor { get; set; }
    }
}