using System;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class RpcCompatDriver : IConformanceDriver
    {
        public static readonly RpcCompatDriver Instance = new();

        public string SuiteId => "rpc-compat";
        public string SuiteName => "Nethereum ethereum/execution-apis rpc-compat (in-process JSON-RPC)";

        public async Task<ConformanceCaseResult> RunAsync(RpcReferenceChain chain, RpcCompatVectorLoader.Exchange exchange)
        {
            RpcRequestMessage request;
            JObject expected;
            try
            {
                request = ParseRequest(exchange.RequestJson);
                expected = JObject.Parse(exchange.ExpectedResponseJson);
            }
            catch (Exception ex)
            {
                return ConformanceCaseResult.Fail("malformedVector", $"{ex.GetType().Name}: {ex.Message}");
            }

            var response = await chain.Dispatcher.DispatchAsync(request).ConfigureAwait(false);

            var expectsError = expected["error"] != null;

            if (response.HasError)
            {
                if (response.Error.Code == -32601)
                    return ConformanceCaseResult.Fail("not-implemented",
                        $"method '{request.Method}' has no handler on the node");

                if (expectsError)
                    return ConformanceCaseResult.Ok();

                return ConformanceCaseResult.Fail("servedError",
                    $"expected a result but node returned error {response.Error.Code}: {response.Error.Message}");
            }

            if (expectsError)
                return ConformanceCaseResult.Fail("errorNotEnforced",
                    $"expected error {expected["error"]?["code"]} but node served a result");

            var actualResult = ServedResult(response);
            var expectedResult = expected["result"] ?? JValue.CreateNull();

            if (exchange.SchemaOnly)
            {
                if (ShapeConforms(expectedResult, actualResult))
                    return ConformanceCaseResult.Ok();

                return ConformanceCaseResult.Fail("schemaMismatch",
                    $"method '{request.Method}' (speconly): served {Truncate(actualResult)} does not match expected shape {Truncate(expectedResult)}");
            }

            if (JToken.DeepEquals(Canonicalize(actualResult), Canonicalize(expectedResult)))
                return ConformanceCaseResult.Ok();

            return ConformanceCaseResult.Fail("resultMismatch",
                $"method '{request.Method}': served {Truncate(actualResult)} != expected {Truncate(expectedResult)}");
        }

        private static RpcRequestMessage ParseRequest(string requestJson)
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize(requestJson, CoreChainJsonContext.Default.JsonRpcRequest);
            if (parsed == null)
                throw new System.Text.Json.JsonException("vector request parsed to a null JSON-RPC request");

            return parsed.ToRpcRequestMessage();
        }

        private static JToken ServedResult(RpcResponseMessage response)
        {
            var wire = System.Text.Json.JsonSerializer.Serialize(response.ToJsonRpcResponse(), CoreChainJsonContext.Default.Options);
            using var doc = JsonDocument.Parse(wire);
            return doc.RootElement.TryGetProperty("result", out var result)
                ? JToken.Parse(result.GetRawText())
                : JValue.CreateNull();
        }

        private static JToken Canonicalize(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var obj = new JObject();
                    foreach (var prop in (JObject)token)
                        obj[prop.Key] = Canonicalize(prop.Value);
                    return obj;
                case JTokenType.Array:
                    var arr = new JArray();
                    foreach (var item in (JArray)token)
                        arr.Add(Canonicalize(item));
                    return arr;
                case JTokenType.String:
                    var s = (string)token;
                    return s != null && s.StartsWith("0x") ? s.ToLowerInvariant() : s;
                default:
                    return token;
            }
        }

        internal static bool ShapeConforms(JToken expected, JToken actual)
            => ShapeConforms(expected, actual, requireKeys: true);

        private static bool ShapeConforms(JToken expected, JToken actual, bool requireKeys)
        {
            if (expected == null || expected.Type == JTokenType.Null)
                return true;

            if (actual == null) return false;
            if (BaseType(expected) != BaseType(actual)) return false;

            switch (expected.Type)
            {
                case JTokenType.Object:
                    var eo = (JObject)expected;
                    var ao = (JObject)actual;
                    foreach (var prop in eo)
                    {
                        var av = ao[prop.Key];
                        if (av == null)
                        {
                            if (requireKeys) return false;
                            continue;
                        }
                        if (!ShapeConforms(prop.Value, av, requireKeys: false)) return false;
                    }
                    return true;
                case JTokenType.Array:
                    var ea = (JArray)expected;
                    var aa = (JArray)actual;
                    if (ea.Count == 0 || aa.Count == 0) return true;
                    return ShapeConforms(ea[0], aa[0], requireKeys: false);
                default:
                    return true;
            }
        }

        private static JTokenType BaseType(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    return JTokenType.Float;
                case JTokenType.String:
                case JTokenType.Date:
                case JTokenType.Guid:
                case JTokenType.Uri:
                case JTokenType.TimeSpan:
                    return JTokenType.String;
                default:
                    return token.Type;
            }
        }

        private static string Truncate(JToken token)
        {
            var s = token.ToString(Formatting.None);
            return s.Length > 200 ? s.Substring(0, 200) + "..." : s;
        }
    }
}
