using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc
{
    public static class JsonRpcMessageExtensions
    {
        public static RpcRequestMessage ToRpcRequestMessage(this JsonRpcRequest request)
        {
            return new RpcRequestMessage
            {
                Id = request.Id,
                Method = request.Method,
                JsonRpcVersion = request.Jsonrpc,
                RawParameters = request.Params.HasValue ? request.Params.Value : null
            };
        }

        public static bool IsNotification(this System.Text.Json.JsonElement member) =>
            member.ValueKind == System.Text.Json.JsonValueKind.Object &&
            member.TryGetProperty("method", out var method) &&
            method.ValueKind == System.Text.Json.JsonValueKind.String &&
            !member.TryGetProperty("id", out _);

        public static RpcRequestMessage? ReadRequestMember(this System.Text.Json.JsonElement member)
        {
            if (member.ValueKind != System.Text.Json.JsonValueKind.Object) return null;

            var message = new RpcRequestMessage { JsonRpcVersion = "2.0" };
            var hasMethod = false;
            try
            {
                foreach (var property in member.EnumerateObject())
                {
                    switch (property.Name)
                    {
                        case "method":
                            if (property.Value.ValueKind != System.Text.Json.JsonValueKind.String) return null;
                            message.Method = property.Value.GetString();
                            hasMethod = true;
                            break;
                        case "jsonrpc":
                            if (property.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                                message.JsonRpcVersion = property.Value.GetString();
                            break;
                        case "id":
                            switch (property.Value.ValueKind)
                            {
                                case System.Text.Json.JsonValueKind.Null:
                                    message.Id = null;
                                    break;
                                case System.Text.Json.JsonValueKind.Number:
                                    message.Id = property.Value.Clone();
                                    break;
                                case System.Text.Json.JsonValueKind.String:
                                    message.Id = property.Value.GetRpcString();
                                    break;
                                default:
                                    return null;
                            }
                            break;
                        case "params":
                            message.RawParameters = property.Value.ValueKind == System.Text.Json.JsonValueKind.Null
                                ? null
                                : property.Value.Clone();
                            break;
                    }
                }
            }
            catch (System.InvalidOperationException)
            {
                return null;
            }
            catch (RpcException)
            {
                return null;
            }

            return hasMethod ? message : null;
        }

        public static string SerializeResponse(this RpcResponseMessage response, System.Text.Json.JsonSerializerOptions options)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Serialize(response.ToJsonRpcResponse(), options);
            }
            catch (System.Exception)
            {
                return System.Text.Json.JsonSerializer.Serialize(
                    new JsonRpcResponse { Id = null, Error = new JsonRpcError { Code = -32603, Message = "Internal error" } },
                    options);
            }
        }

        public static string SerializeResponses(
            this System.Collections.Generic.IEnumerable<RpcResponseMessage> responses, System.Text.Json.JsonSerializerOptions options) =>
            "[" + string.Join(",", System.Linq.Enumerable.Select(responses, r => r.SerializeResponse(options))) + "]";

        public static JsonRpcResponse ToJsonRpcResponse(this RpcResponseMessage response)
        {
            if (response.HasError)
            {
                return new JsonRpcResponse
                {
                    Id = response.Id,
                    Error = new JsonRpcError
                    {
                        Code = response.Error.Code,
                        Message = response.Error.Message,
                        Data = response.Error.Data
                    }
                };
            }

            return new JsonRpcResponse
            {
                Id = response.Id,
                Result = response.Result
            };
        }
    }
}
