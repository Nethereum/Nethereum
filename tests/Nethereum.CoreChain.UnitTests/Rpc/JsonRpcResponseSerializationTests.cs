using System.Text.Json;
using Nethereum.CoreChain.Rpc;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class JsonRpcResponseSerializationTests
    {
        public static readonly object[][] SerializerModes =
        {
            new object[] { new JsonSerializerOptions() },
            new object[] { CoreChainJsonContext.Default.Options }
        };

        [Theory]
        [MemberData(nameof(SerializerModes))]
        public void SuccessResponse_WithNullResult_SerializesResultNull_NoErrorKey(JsonSerializerOptions options)
        {
            var response = new JsonRpcResponse { Id = 1L, Result = null, Error = null };

            var json = JsonSerializer.Serialize(response, options);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.True(root.TryGetProperty("result", out var resultProperty),
                "a success response must always carry \"result\", even when the value is null");
            Assert.Equal(JsonValueKind.Null, resultProperty.ValueKind);
            Assert.False(root.TryGetProperty("error", out _),
                "a success response must never carry \"error\"");
        }

        [Theory]
        [MemberData(nameof(SerializerModes))]
        public void SuccessResponse_WithComplexObjectResult_SerializesRuntimeType_NotEmptyObject(JsonSerializerOptions options)
        {
            var response = new JsonRpcResponse
            {
                Id = 1L,
                Result = new AccessList { Address = "0xabc", StorageKeys = new System.Collections.Generic.List<string> { "0x1" } },
                Error = null
            };

            var json = JsonSerializer.Serialize(response, options);
            using var document = JsonDocument.Parse(json);
            var result = document.RootElement.GetProperty("result");

            Assert.Equal(JsonValueKind.Object, result.ValueKind);
            Assert.True(result.TryGetProperty("address", out var address),
                "the concrete DTO's fields must serialise, not an empty object");
            Assert.Equal("0xabc", address.GetString());
        }

        [Theory]
        [MemberData(nameof(SerializerModes))]
        public void SuccessResponse_WithNonNullResult_SerializesResult_NoErrorKey(JsonSerializerOptions options)
        {
            var response = new JsonRpcResponse { Id = 1L, Result = "0xdeadbeef", Error = null };

            var json = JsonSerializer.Serialize(response, options);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.True(root.TryGetProperty("result", out var resultProperty));
            Assert.Equal("0xdeadbeef", resultProperty.GetString());
            Assert.False(root.TryGetProperty("error", out _));
        }

        [Theory]
        [MemberData(nameof(SerializerModes))]
        public void ErrorResponse_SerializesError_NoResultKey(JsonSerializerOptions options)
        {
            var response = new JsonRpcResponse
            {
                Id = 1L,
                Result = null,
                Error = new JsonRpcError { Code = -32601, Message = "Method not found" }
            };

            var json = JsonSerializer.Serialize(response, options);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.True(root.TryGetProperty("error", out var errorProperty),
                "an error response must always carry \"error\"");
            Assert.Equal(-32601, errorProperty.GetProperty("code").GetInt32());
            Assert.Equal("Method not found", errorProperty.GetProperty("message").GetString());
            Assert.False(root.TryGetProperty("result", out _),
                "an error response must never carry \"result\"");
        }

        [Fact]
        public void ErrorResponse_DataOmittedWhenNull()
        {
            var response = new JsonRpcResponse
            {
                Id = 1L,
                Error = new JsonRpcError { Code = -32602, Message = "Invalid params", Data = null }
            };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions());
            using var document = JsonDocument.Parse(json);
            var errorProperty = document.RootElement.GetProperty("error");

            Assert.False(errorProperty.TryGetProperty("data", out _),
                "error.data should still be omitted when null - only the result/error XOR changed");
        }

        [Fact]
        public void JsonRpcAndId_AlwaysPresent()
        {
            var response = new JsonRpcResponse { Id = null, Result = null };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions());
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
            Assert.True(root.TryGetProperty("id", out var idProperty));
            Assert.Equal(JsonValueKind.Null, idProperty.ValueKind);
        }

        [Fact]
        public void SuccessResponse_RoundTripsThroughDeserialization()
        {
            var original = new JsonRpcResponse { Id = 7L, Result = "0xabc" };
            var json = JsonSerializer.Serialize(original, new JsonSerializerOptions());

            var roundTripped = JsonSerializer.Deserialize<JsonRpcResponse>(json, new JsonSerializerOptions());

            Assert.NotNull(roundTripped);
            Assert.Null(roundTripped!.Error);
            Assert.Equal(7L, roundTripped.Id);
        }

        [Fact]
        public void ErrorResponse_RoundTripsThroughDeserialization()
        {
            var original = new JsonRpcResponse
            {
                Id = 2L,
                Error = new JsonRpcError { Code = -32700, Message = "Parse error" }
            };
            var json = JsonSerializer.Serialize(original, new JsonSerializerOptions());

            var roundTripped = JsonSerializer.Deserialize<JsonRpcResponse>(json, new JsonSerializerOptions());

            Assert.NotNull(roundTripped);
            Assert.Null(roundTripped!.Result);
            Assert.NotNull(roundTripped.Error);
            Assert.Equal(-32700, roundTripped.Error!.Code);
            Assert.Equal("Parse error", roundTripped.Error.Message);
        }

        [Fact]
        public void ToJsonRpcResponse_MapsSuccessWithNullResult_ToSuccessInvariant()
        {
            var dispatcherResponse = new Nethereum.JsonRpc.Client.RpcMessages.RpcResponseMessage(1L, (object?)null);

            var wireResponse = dispatcherResponse.ToJsonRpcResponse();
            var json = JsonSerializer.Serialize(wireResponse, new JsonSerializerOptions());
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.True(root.TryGetProperty("result", out var resultProperty));
            Assert.Equal(JsonValueKind.Null, resultProperty.ValueKind);
            Assert.False(root.TryGetProperty("error", out _));
        }

        [Fact]
        public void ToJsonRpcResponse_MapsSuccessWithResult_ToResultOnly()
        {
            var dispatcherResponse = new Nethereum.JsonRpc.Client.RpcMessages.RpcResponseMessage(7L, "0xabc");

            var wireResponse = dispatcherResponse.ToJsonRpcResponse();
            var json = JsonSerializer.Serialize(wireResponse, new JsonSerializerOptions());
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.Equal(7L, root.GetProperty("id").GetInt64());
            Assert.Equal("0xabc", root.GetProperty("result").GetString());
            Assert.False(root.TryGetProperty("error", out _));
        }

        [Fact]
        public void ToJsonRpcResponse_MapsError_ToErrorWithCodeMessageData_NoResult()
        {
            var dispatcherError = new Nethereum.JsonRpc.Client.RpcMessages.RpcError
            {
                Code = -32602,
                Message = "Invalid params",
                Data = "0xdead"
            };
            var dispatcherResponse = new Nethereum.JsonRpc.Client.RpcMessages.RpcResponseMessage(3L, dispatcherError);

            var wireResponse = dispatcherResponse.ToJsonRpcResponse();
            var json = JsonSerializer.Serialize(wireResponse, new JsonSerializerOptions());
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.Equal(3L, root.GetProperty("id").GetInt64());
            Assert.False(root.TryGetProperty("result", out _));
            var error = root.GetProperty("error");
            Assert.Equal(-32602, error.GetProperty("code").GetInt32());
            Assert.Equal("Invalid params", error.GetProperty("message").GetString());
            Assert.Equal("0xdead", error.GetProperty("data").GetString());
        }
    }
}
