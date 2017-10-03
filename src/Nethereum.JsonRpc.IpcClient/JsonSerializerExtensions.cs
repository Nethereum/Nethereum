using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.JsonRpc.IpcClient
{
    public static class JsonSerializerExtensions
    {
        public static void CopySerializerSettings(this JsonSerializer jsonSerializer, JsonSerializerSettings serializerSettings)
        {
            if (serializerSettings != null)
            {
                jsonSerializer.ConstructorHandling = serializerSettings.ConstructorHandling;
                jsonSerializer.CheckAdditionalContent = serializerSettings.CheckAdditionalContent;
                jsonSerializer.DateFormatHandling = serializerSettings.DateFormatHandling;
                jsonSerializer.DateFormatString = serializerSettings.DateFormatString;
                jsonSerializer.DateParseHandling = serializerSettings.DateParseHandling;
                jsonSerializer.DateTimeZoneHandling = serializerSettings.DateTimeZoneHandling;
                jsonSerializer.DefaultValueHandling = serializerSettings.DefaultValueHandling;
                jsonSerializer.EqualityComparer = serializerSettings.EqualityComparer;
                jsonSerializer.FloatFormatHandling = serializerSettings.FloatFormatHandling;
                jsonSerializer.Formatting = serializerSettings.Formatting;
                jsonSerializer.FloatParseHandling = serializerSettings.FloatParseHandling;
                jsonSerializer.MaxDepth = serializerSettings.MaxDepth;
                jsonSerializer.MetadataPropertyHandling = serializerSettings.MetadataPropertyHandling;
                jsonSerializer.MissingMemberHandling = serializerSettings.MissingMemberHandling;
                jsonSerializer.NullValueHandling = serializerSettings.NullValueHandling;
                jsonSerializer.ObjectCreationHandling = serializerSettings.ObjectCreationHandling;
                jsonSerializer.PreserveReferencesHandling = serializerSettings.PreserveReferencesHandling;
                jsonSerializer.ReferenceLoopHandling = serializerSettings.ReferenceLoopHandling;
                jsonSerializer.StringEscapeHandling = serializerSettings.StringEscapeHandling;
                jsonSerializer.TraceWriter = serializerSettings.TraceWriter;
                jsonSerializer.TypeNameHandling = serializerSettings.TypeNameHandling;
            }
        }
    }
}
