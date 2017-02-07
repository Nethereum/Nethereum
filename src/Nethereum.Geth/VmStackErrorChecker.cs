using Newtonsoft.Json.Linq;

namespace Nethereum.Geth
{
    public class VmStackErrorChecker
    {
        public string GetError(JObject stack)
        {
            var structsLogs = (JArray) stack["structLogs"];
            if (structsLogs.Count > 0)
            {
                var lastCall = structsLogs[structsLogs.Count - 1];
                return lastCall["error"].Value<string>();
            }
            return null;
        }

        public bool HasError(JObject stack)
        {
            return !string.IsNullOrEmpty(GetError(stack));
        }
    }
}