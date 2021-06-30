
namespace Nethereum.RPC.Eth.DTOs
{
    using System;

    //#if NETHLITE
    //    using System.Text.Json;
    //    using System.Text.Json.Serialization;

    //    public class BlockParameterJsonConverter : JsonConverter<BlockParameter>
    //    {

    //        public override bool CanConvert(Type objectType)
    //        {
    //            return objectType == typeof (BlockParameter);
    //        }

    //        public override BlockParameter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    //        {
    //            throw new NotImplementedException();
    //        }

    //        public override void Write(Utf8JsonWriter writer, BlockParameter value, JsonSerializerOptions options)
    //        {
    //            var blockParameter = (BlockParameter)value;

    //            writer.WriteStringValue(blockParameter.GetRPCParam());
    //        }
    //    }
    //}
    //#else

    using Newtonsoft.Json;

    public class BlockParameterJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(BlockParameter);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var blockParameter = (BlockParameter)value;

            writer.WriteValue(blockParameter.GetRPCParam());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

}