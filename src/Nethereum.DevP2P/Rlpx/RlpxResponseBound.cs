using System;
using Nethereum.RLP;

namespace Nethereum.DevP2P.Rlpx
{
    public enum RlpxResponseBoundKind
    {
        Unbounded,
        Items,
        ContentBytes
    }

    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.DevP2P, "devp2p", "RlpxResponseBound — cap what a response may hold")]
    public readonly struct RlpxResponseBound
    {
        private RlpxResponseBound(RlpxResponseBoundKind kind, long limit)
        {
            Kind = kind;
            Limit = limit;
        }

        public RlpxResponseBoundKind Kind { get; }
        public long Limit { get; }

        public static RlpxResponseBound Unbounded => default;

        public static RlpxResponseBound Items(long maxItems)
        {
            return new RlpxResponseBound(RlpxResponseBoundKind.Items, maxItems);
        }

        public static RlpxResponseBound ContentBytes(long maxBytes)
        {
            return maxBytes <= 0 ? default : new RlpxResponseBound(RlpxResponseBoundKind.ContentBytes, maxBytes);
        }
    }

    internal static class RlpxResponseEnvelope
    {
        public static bool TryRead(byte[] payload, RlpxResponseBound bound, out ulong requestId, out string violation)
        {
            requestId = 0;
            violation = null;
            try
            {
                var reader = new RlpReader(payload);
                reader.ReadListHeader();
                requestId = reader.ReadUInt64();
                if (bound.Kind == RlpxResponseBoundKind.Unbounded) return true;

                while (reader.PeekKind() != RlpKind.List)
                    reader.Skip();
                var items = reader.ReadListHeader();
                if (bound.Kind == RlpxResponseBoundKind.Items)
                {
                    var count = reader.CountItems(items);
                    if (count > bound.Limit)
                        violation = "response holds " + count + " items for a request of " + bound.Limit;
                }
                else if (items.Length > bound.Limit)
                {
                    violation = "response holds " + items.Length + " content bytes for a request of " + bound.Limit;
                }
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public static bool TryReadRequestId(byte[] payload, out ulong requestId)
        {
            return TryRead(payload, RlpxResponseBound.Unbounded, out requestId, out _);
        }
    }
}
