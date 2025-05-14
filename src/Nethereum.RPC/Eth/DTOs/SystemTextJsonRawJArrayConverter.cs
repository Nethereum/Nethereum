#if NET6_0_OR_GREATER
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;

public class SystemTextJsonRawJArrayConverter : JsonConverter<JArray>
{
    public override JArray Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var raw = doc.RootElement.GetRawText();
        return JArray.Parse(raw);
    }

    public override void Write(Utf8JsonWriter writer, JArray value, JsonSerializerOptions options)
    {
        writer.WriteRawValue(value.ToString(Newtonsoft.Json.Formatting.None));
    }
}
#endif