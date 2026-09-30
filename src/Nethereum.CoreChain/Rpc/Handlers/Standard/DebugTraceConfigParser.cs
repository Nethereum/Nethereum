using System.Text.Json;
using Nethereum.CoreChain.Tracing;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    internal static class DebugTraceConfigParser
    {
        public static OpcodeTraceConfig ParseOpcodeConfig(JsonElement element)
        {
            var config = new OpcodeTraceConfig();
            ApplyFlags(element, config);
            if (element.TryGetProperty("tracerConfig", out var tracerConfigProp))
                ApplyFlags(tracerConfigProp, config);
            return config;
        }

        private static void ApplyFlags(JsonElement element, OpcodeTraceConfig config)
        {
            if (element.TryGetProperty("enableMemory", out var enableMemoryProp))
                config.EnableMemory = enableMemoryProp.GetBoolean();

            if (element.TryGetProperty("disableStack", out var disableStackProp))
                config.DisableStack = disableStackProp.GetBoolean();

            if (element.TryGetProperty("disableStorage", out var disableStorageProp))
                config.DisableStorage = disableStorageProp.GetBoolean();

            if (element.TryGetProperty("enableReturnData", out var enableReturnDataProp))
                config.EnableReturnData = enableReturnDataProp.GetBoolean();

            if (element.TryGetProperty("limit", out var limitProp))
                config.Limit = limitProp.GetInt32();
        }
    }
}
