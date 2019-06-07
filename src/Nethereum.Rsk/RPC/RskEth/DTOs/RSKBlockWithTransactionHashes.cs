using System.Numerics;
using Nethereum.ABI.Util;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;

namespace Nethereum.Rsk.RPC.RskEth.DTOs
{
    public class RskBlockWithTransactionHashes : BlockWithTransactionHashes, IRskBlockExtended
    {
        /// <summary>
        ///     QUANTITY - the minimum gas price in Wei
        /// </summary>
        [JsonProperty(PropertyName = "minimumGasPrice")]
        public string MinimumGasPriceString { get; set; }

        
        

    }

}