#if NET6_0_OR_GREATER
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public class BlockParameterSystemTextJsonConverter : JsonConverter<BlockParameter>
{
    public override BlockParameter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // You can implement this later based on your logic (e.g., "latest", hex string, etc.)
        throw new NotImplementedException("Deserialization is not implemented for BlockParameter.");
    }

    public override void Write(Utf8JsonWriter writer, BlockParameter value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.GetRPCParam());
    }
}
#endif