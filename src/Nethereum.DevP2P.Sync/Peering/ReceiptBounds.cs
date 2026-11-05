using System.Collections.Generic;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Peering
{
    internal static class ReceiptBounds
    {
        internal const ulong MinTransactionGas = 4500;
        internal const ulong LogDataGas = 8;
        internal const ulong UnknownBlockGasUsed = 300_000_000;

        internal static bool Exceeded(List<Receipt> partial, ulong gasUsed)
            => (ulong)partial.Count > gasUsed / MinTransactionGas
               || LogsSize(partial) > gasUsed / LogDataGas;

        private static ulong LogsSize(List<Receipt> receipts)
        {
            ulong size = 0;
            foreach (var receipt in receipts)
            {
                if (receipt.Logs == null) continue;
                foreach (var log in receipt.Logs)
                {
                    var topics = (ulong)(log.Topics?.Count ?? 0) * 33;
                    var payload = 21 + ListSize(topics) + StringSize((ulong)(log.Data?.Length ?? 0));
                    size += ListSize(payload);
                }
            }
            return size;
        }

        private static ulong ListSize(ulong payload) => payload + HeaderSize(payload);

        private static ulong StringSize(ulong length) => length == 1 ? 1 : length + HeaderSize(length);

        private static ulong HeaderSize(ulong payload)
        {
            if (payload <= 55) return 1;
            ulong lengthBytes = 0;
            for (var rest = payload; rest != 0; rest >>= 8) lengthBytes++;
            return 1 + lengthBytes;
        }
    }
}
