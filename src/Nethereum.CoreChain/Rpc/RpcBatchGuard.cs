using System.Collections.Generic;
using System.Text.Json;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc
{
    public static class RpcBatchGuard
    {
        public static List<RpcResponseMessage>? RejectOversizedBatch(System.ReadOnlySpan<byte> utf8Body, int maxBatchSize)
        {
            try
            {
                var reader = new Utf8JsonReader(utf8Body);
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray) return null;

                var count = 0;
                object? firstId = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    count++;
                    if (count == 1 && reader.TokenType == JsonTokenType.StartObject)
                    {
                        firstId = ReadMemberId(ref reader);
                    }
                    else if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                    {
                        reader.Skip();
                    }

                    if (count > maxBatchSize) return RpcDispatcher.BatchTooLarge(firstId);
                }
            }
            catch (JsonException)
            {
            }

            return null;
        }

        private static object? ReadMemberId(ref Utf8JsonReader reader)
        {
            object? id = null;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == 1) break;
                if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 2) continue;

                var isId = reader.ValueTextEquals("id");
                reader.Read();
                if (isId)
                {
                    id = reader.TokenType switch
                    {
                        JsonTokenType.Number => reader.TryGetInt64(out var number) ? (object)number : reader.GetDouble(),
                        JsonTokenType.String => reader.GetString(),
                        _ => null
                    };
                }

                if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                {
                    reader.Skip();
                }
            }

            return id;
        }
    }
}
