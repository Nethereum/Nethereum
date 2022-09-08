using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;
using System.Text;
using System.IO;
using Nethereum.JsonRpc.Client.RpcMessages;



namespace Nethereum.JsonRpc.IpcClient
{
    public abstract class IpcClientBase : ClientBase, IDisposable
    {
        protected readonly string IpcPath;
        public static int ForceCompleteReadTotalMiliseconds { get; set; } = 2000;

        public IpcClientBase(string ipcPath, JsonSerializerSettings jsonSerializerSettings = null)
        {
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            this.IpcPath = ipcPath;
            JsonSerializerSettings = jsonSerializerSettings;
        }

        public JsonSerializerSettings JsonSerializerSettings { get; set; }

     
        public string ReadJson(JsonReader reader)
        {
            var sb = new StringBuilder();
            var sw = new StringWriter(sb);

            using (var writer = new JsonTextWriter(sw))
            {
                if (reader.TokenType == JsonToken.StartObject)
                {
                    int depth = 1;
                    writer.WriteStartObject();

                    while (depth > 0)
                    {
                        if (!TryRead(reader))
                            break;
                        switch (reader.TokenType)
                        {
                            case JsonToken.PropertyName:
                                writer.WritePropertyName(reader.Value.ToString());
                                break;
                            case JsonToken.StartArray:
                                writer.WriteStartArray();
                                break;
                            case JsonToken.Boolean:
                            case JsonToken.Integer:
                            case JsonToken.Float:
                            case JsonToken.String:
                            case JsonToken.Date:
                            case JsonToken.Null:
                                writer.WriteValue(reader.Value);
                                break;
                            case JsonToken.EndArray:
                                writer.WriteEndArray();
                                break;
                            case JsonToken.StartObject:
                                writer.WriteStartObject();
                                depth++;
                                break;
                            case JsonToken.EndObject:
                                writer.WriteEndObject();
                                depth--;
                                break;
                        }
                    }
                    while (depth > 0)
                    {
                        depth--;
                        writer.WriteEndObject();
                    }
                }
            }
            return sb.ToString();
        }

        public bool TryRead(JsonReader jsonReader)
        {
            if (Task.Run(() => jsonReader.Read()).Wait(1000))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public override Task<RpcRequestResponseBatch> SendBatchRequestAsync(RpcRequestResponseBatch rpcRequestResponseBatch)
        {
            throw new NotImplementedException();
        }

        protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests)
        {
            throw new NotImplementedException();
        }

        #region IDisposable Support

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
