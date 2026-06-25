using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.CoreChain.Sync
{
    /// <summary>
    /// Converts wire-shaped <see cref="Withdrawal"/>s (raw 20-byte address
    /// + ulong gwei) to engine-shaped <see cref="WithdrawalEntry"/>s
    /// (0x-prefixed hex address + BigInteger gwei) consumed by
    /// <see cref="BlockImporter.ImportAsync"/> and
    /// <see cref="BlockExecutor.ExecuteAsync"/>. Returns <c>null</c> on
    /// null / empty input so pre-Shanghai callers match the
    /// optional-parameter contract.
    /// </summary>
    public static class WithdrawalAdapter
    {
        public static IList<WithdrawalEntry> Convert(IList<Withdrawal> wlist)
        {
            if (wlist == null || wlist.Count == 0) return null;
            var result = new List<WithdrawalEntry>(wlist.Count);
            foreach (var w in wlist)
            {
                var addr = "0x" + w.Address.ToHex();
                result.Add(new WithdrawalEntry(addr, w.AmountInGwei));
            }
            return result;
        }
    }
}
