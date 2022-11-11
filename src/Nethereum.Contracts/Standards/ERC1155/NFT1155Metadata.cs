using Nethereum.Contracts.Standards.ERC721;
using Newtonsoft.Json;

namespace Nethereum.Contracts.Standards.ERC1155
{
    public class NFT1155Metadata : NftMetadata
    {

        [JsonProperty("decimals")]
        public int Decimals { get; set; }

    }
}