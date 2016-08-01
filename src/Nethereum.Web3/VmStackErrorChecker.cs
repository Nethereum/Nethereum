using Newtonsoft.Json.Linq;

namespace Nethereum.Web3
{
    public class VmStackErrorChecker
    {
        public bool HasError(JObject stack)
        {
            return !string.IsNullOrEmpty(GetError(stack));
        }

        public string GetError(JObject stack)
        {
            var structsLogs = (JArray)stack["structLogs"];
            if (structsLogs.Count > 0)
            {
                var lastCall = structsLogs[structsLogs.Count - 1];
                return lastCall["error"].Value<string>();
            }
            return null;
        }
    }
}