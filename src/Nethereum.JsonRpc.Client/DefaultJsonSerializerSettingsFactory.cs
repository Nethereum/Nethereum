using Newtonsoft.Json;

namespace Nethereum.JsonRpc.Client
{
    public static class DefaultJsonSerializerSettingsFactory
    {
        public static JsonSerializerSettings BuildDefaultJsonSerializerSettings()
        {
            return new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        }
    }
}