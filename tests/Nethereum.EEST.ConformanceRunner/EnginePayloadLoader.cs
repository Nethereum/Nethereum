using System.Collections.Generic;
using Nethereum.RPC.Eth.DTOs.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.EEST.ConformanceRunner
{
    public static class EnginePayloadLoader
    {
        public sealed class EnginePayloadEntry
        {
            public int Version { get; init; }
            public ExecutionPayloadV3 Payload { get; init; }
            public ExecutionPayloadV4 PayloadV4 { get; init; }
            public string ParentBeaconBlockRoot { get; init; }
            public string[] ExecutionRequests { get; init; }
            public string ValidationError { get; init; }
            public bool HasErrorCode { get; init; }
            public int? ErrorCode { get; init; }

            public string RequestJson { get; init; }

            public bool IsNegative => !string.IsNullOrEmpty(ValidationError) || HasErrorCode;
        }

        public static List<EnginePayloadEntry> ReadEntries(JObject test)
        {
            var entries = new List<EnginePayloadEntry>();
            if (test["engineNewPayloads"] is not JArray payloads)
                return entries;

            foreach (var entryToken in payloads)
            {
                var entry = (JObject)entryToken;
                var version = entry.Value<int>("newPayloadVersion");
                var parameters = (JArray)entry["params"];

                var payloadToken = parameters[0];
                var beaconRoot = parameters.Count > 2 ? parameters[2]?.Value<string>() : null;
                var executionRequests = parameters.Count > 3 && parameters[3] is JArray requests
                    ? requests.ToObject<string[]>()
                    : null;

                var requestJson = new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = 1,
                    ["method"] = $"engine_newPayloadV{version}",
                    ["params"] = parameters.DeepClone()
                }.ToString(Formatting.None);

                entries.Add(new EnginePayloadEntry
                {
                    Version = version,
                    Payload = version >= 5
                        ? null
                        : payloadToken.ToObject<ExecutionPayloadV3>(),
                    PayloadV4 = version >= 5
                        ? payloadToken.ToObject<ExecutionPayloadV4>()
                        : null,
                    ParentBeaconBlockRoot = beaconRoot,
                    ExecutionRequests = executionRequests,
                    ValidationError = entry.Value<string>("validationError"),
                    HasErrorCode = entry["errorCode"] != null,
                    ErrorCode = entry["errorCode"] != null ? int.Parse(entry.Value<string>("errorCode")) : (int?)null,
                    RequestJson = requestJson
                });
            }

            return entries;
        }
    }
}
