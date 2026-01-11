using System.Collections.Generic;
using System.Text.Json.Serialization;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(CallInput))]
    [JsonSerializable(typeof(NewFilterInput))]
    [JsonSerializable(typeof(HexBigInteger))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(string[]))]
    [JsonSerializable(typeof(bool))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(long))]
    [JsonSerializable(typeof(double))]
    [JsonSerializable(typeof(object))]
    [JsonSerializable(typeof(object[]))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(List<double>))]
    [JsonSerializable(typeof(List<object>))]
    public partial class CoreChainJsonContext : JsonSerializerContext
    {
    }
}
