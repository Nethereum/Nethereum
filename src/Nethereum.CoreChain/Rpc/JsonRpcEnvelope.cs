using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc
{
    public sealed class JsonRpcEnvelopeResult
    {
        public JsonRpcEnvelopeResult(int status, string? body)
        {
            Status = status;
            Body = body;
        }

        public int Status { get; }

        public string? Body { get; }
    }

    public sealed class JsonRpcEnvelope
    {
        public const long DefaultMaxResponseSize = 25 * 1000 * 1000;
        public const int ResponseTooLargeCode = -32003;

        private readonly IRpcDispatcher _dispatcher;
        private readonly JsonSerializerOptions _options;
        private readonly ILogger? _logger;
        private readonly long _maxResponseSize;

        public JsonRpcEnvelope(
            IRpcDispatcher dispatcher,
            JsonSerializerOptions options,
            ILogger? logger = null,
            long maxResponseSize = DefaultMaxResponseSize)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
            _maxResponseSize = maxResponseSize;
        }

        public static JsonSerializerOptions CreateSerializerOptions() => new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = CoreChainJsonContext.Default
        };

        public async Task<JsonRpcEnvelopeResult> HandleAsync(ReadOnlyMemory<byte> utf8Body, CancellationToken cancellationToken)
        {
            var rejection = RpcBatchGuard.RejectOversizedBatch(utf8Body.Span, _dispatcher.MaxBatchSize);
            if (rejection != null)
            {
                return new JsonRpcEnvelopeResult(200, rejection.SerializeResponses(_options));
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(utf8Body);
            }
            catch (JsonException)
            {
                return Error(-32700, "Parse error");
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    return await HandleBatchAsync(root, cancellationToken).ConfigureAwait(false);
                }

                var request = root.ReadRequestMember();
                if (request == null || string.IsNullOrEmpty(request.Method))
                {
                    return Error(-32600, "Invalid Request");
                }

                var response = await DispatchAsync(request).WaitAsync(cancellationToken).ConfigureAwait(false);
                if (root.IsNotification())
                {
                    return new JsonRpcEnvelopeResult(204, null);
                }

                return new JsonRpcEnvelopeResult(200, response.SerializeResponse(_options));
            }
        }

        private async Task<JsonRpcEnvelopeResult> HandleBatchAsync(JsonElement batch, CancellationToken cancellationToken)
        {
            if (batch.GetArrayLength() == 0)
            {
                return Error(-32600, "Invalid Request");
            }

            var members = new List<JsonElement>(batch.EnumerateArray());
            var requests = members.ConvertAll(member => member.ReadRequestMember());
            var notifications = members.ConvertAll(member => member.IsNotification());
            var tooLargeEntries = new string[members.Count];
            var tooLargeFromHere = new long[members.Count + 1];
            for (var i = members.Count - 1; i >= 0; i--)
            {
                tooLargeEntries[i] = notifications[i] ? string.Empty : ResponseTooLarge(requests[i]?.Id).SerializeResponse(_options);
                tooLargeFromHere[i] = tooLargeFromHere[i + 1] + (notifications[i] ? 0 : tooLargeEntries[i].Length + 1);
            }

            var entries = new List<string>();
            long responseSize = 0;
            var exceeded = false;

            for (var i = 0; i < members.Count; i++)
            {
                if (!exceeded)
                {
                    var response = requests[i] == null
                        ? new RpcResponseMessage(null, new RpcError { Code = -32600, Message = "Invalid Request" })
                        : await DispatchAsync(requests[i]).WaitAsync(cancellationToken).ConfigureAwait(false);
                    if (notifications[i]) continue;

                    var serialized = response.SerializeResponse(_options);
                    if (_maxResponseSize > 0 && responseSize + serialized.Length + 1 + tooLargeFromHere[i + 1] > _maxResponseSize)
                    {
                        exceeded = true;
                    }
                    else
                    {
                        entries.Add(serialized);
                        responseSize += serialized.Length + 1;
                        continue;
                    }
                }

                if (!notifications[i]) entries.Add(tooLargeEntries[i]);
            }

            if (entries.Count == 0)
            {
                return new JsonRpcEnvelopeResult(204, null);
            }

            return new JsonRpcEnvelopeResult(200, "[" + string.Join(",", entries) + "]");
        }

        private async Task<RpcResponseMessage> DispatchAsync(RpcRequestMessage request)
        {
            var logger = _logger;
            if (logger != null && logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("{Method}", RpcLogText.Method(request.Method));
            }

            var response = await _dispatcher.DispatchAsync(request).ConfigureAwait(false);
            if (logger != null && response.HasError)
            {
                logger.LogDebug("{Method} -> error {Code}", RpcLogText.Method(request.Method), response.Error.Code);
            }

            return response;
        }

        private static RpcResponseMessage ResponseTooLarge(object? id) =>
            new RpcResponseMessage(id, new RpcError { Code = ResponseTooLargeCode, Message = "response too large" });

        private JsonRpcEnvelopeResult Error(int code, string message)
        {
            var response = new JsonRpcResponse { Id = null, Error = new JsonRpcError { Code = code, Message = message } };
            return new JsonRpcEnvelopeResult(400, JsonSerializer.Serialize(response, _options));
        }
    }
}
