using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nethereum.RPC.AccountAbstraction.DTOs
{
    public class UserOperationReceipt
    {
        [JsonProperty("userOpHash")]
        public string UserOpHash { get; set; }

        [JsonProperty("entryPoint")]
        public string EntryPoint { get; set; }

        [JsonProperty("sender")]
        public string Sender { get; set; }

        [JsonProperty("nonce")]
        public HexBigInteger Nonce { get; set; }

        [JsonProperty("paymaster")]
        public string Paymaster { get; set; }

        [JsonProperty("actualGasCost")]
        public HexBigInteger ActualGasCost { get; set; }

        [JsonProperty("actualGasUsed")]
        public HexBigInteger ActualGasUsed { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("logs")]
        public List<string> Logs { get; set; }

        [JsonProperty("receipt")]
        public TransactionReceipt Receipt { get; set; }
    }
}
