using Newtonsoft.Json.Linq;

namespace Nethereum.JsonRpc.Client
{
    public class RpcError
    {
        public RpcError(int code, string message, object data = null)
        {
            Code = code;
            Message = message;
            Data = data;
        }

        public int Code { get; private set; }
        public string Message { get; private set; }
        public object Data { get; private set; }

        public string GetDataAsString()
        {
#if NET6_0_OR_GREATER
    if (Data is System.Text.Json.Nodes.JsonValue val)
        return val.ToString();

    if (Data is System.Text.Json.Nodes.JsonObject obj)
    {
        if (obj.TryGetPropertyValue("result", out var result) && result is not null)
            return result.ToString();

        if (obj.TryGetPropertyValue("data", out var innerData) && innerData is not null)
            return innerData.ToString();
    }
#endif
         if (Data is JValue jVal && jVal.Type == JTokenType.String)
                return jVal.ToString();

         if (Data is JObject jObj)
         {
            if (jObj["result"] is JToken result)
                return result.ToString();

            if (jObj["data"] is JToken innerData)
                return innerData.ToString();
         }

           if (Data is string str)
                return str;

           if(Data != null)
                return Data.ToString();

            return null;
        }
    }
}