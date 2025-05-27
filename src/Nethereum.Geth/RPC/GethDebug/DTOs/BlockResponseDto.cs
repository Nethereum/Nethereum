using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nethereum.Geth.RPC.Debug.DTOs
{
    public class BlockResponseItemDto<TTracerResponse>
    {
        [JsonProperty(PropertyName = "txHash")]
        public string TxHash { get; set; }
        
        [JsonProperty(PropertyName = "result")]
        public TTracerResponse Result { get; set; }
    }

    public class BlockResponseDto<TTracerResponse> : List<BlockResponseItemDto<TTracerResponse>>
    {
        
    }

    
}