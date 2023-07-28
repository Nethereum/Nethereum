using Newtonsoft.Json;

namespace Nethereum.DataServices.Etherscan.Responses
{
    public class EtherscanGetContractCreatorResponse
    {
        [JsonProperty("contractAddress")]
        public string ContractAddress { get; set; }

        [JsonProperty("contractCreator")]
        public string ContractCreator { get; set; }

        [JsonProperty("txHash")]
        public string TxHash { get; set; }
    }

}
