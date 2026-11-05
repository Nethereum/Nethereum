using System;
using Nethereum.Consensus.LightClient;
using Nethereum.CoreChain.Engine;
using Nethereum.Model;

namespace Nethereum.MainnetChain.Bootstrap
{
    public sealed class EngineForkchoiceHeaders : ITrustedHeaderProvider, IEngineForkchoiceSink
    {
        private volatile TrustedExecutionHeader _head;
        private volatile TrustedExecutionHeader _finalized;

        public string Name => "Engine";

        public void Publish(EngineForkchoiceHead head, EngineForkchoiceHead finalized)
        {
            if (head == null) throw new ArgumentNullException(nameof(head));
            _head = ToTrusted(head);
            if (finalized != null) _finalized = ToTrusted(finalized);
        }

        public TrustedExecutionHeader GetLatestOptimistic() =>
            _head ?? throw new InvalidOperationException("No forkchoiceUpdated has been received yet.");

        public TrustedExecutionHeader GetLatestFinalized() =>
            _finalized ?? throw new InvalidOperationException("No finalized block has been announced by forkchoiceUpdated yet.");

        public byte[] GetBlockHash(ulong blockNumber)
        {
            var head = _head;
            if (head != null && head.BlockNumber == blockNumber) return head.BlockHash;
            var finalized = _finalized;
            if (finalized != null && finalized.BlockNumber == blockNumber) return finalized.BlockHash;
            return null;
        }

        private static TrustedExecutionHeader ToTrusted(EngineForkchoiceHead head) => new TrustedExecutionHeader
        {
            BlockHash = head.Hash,
            BlockNumber = (ulong)head.Header.BlockNumber,
            StateRoot = head.Header.StateRoot ?? Array.Empty<byte>(),
            ReceiptsRoot = head.Header.ReceiptHash ?? Array.Empty<byte>(),
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(head.Header.Timestamp)
        };
    }
}
