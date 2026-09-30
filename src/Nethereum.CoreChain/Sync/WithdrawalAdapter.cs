using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.CoreChain.Sync
{
    public static class WithdrawalAdapter
    {
        public static IList<WithdrawalEntry> Convert(IList<Withdrawal> wlist)
        {
            if (wlist == null || wlist.Count == 0) return null;
            var result = new List<WithdrawalEntry>(wlist.Count);
            foreach (var w in wlist)
            {
                var addr = "0x" + w.Address.ToHex();
                result.Add(new WithdrawalEntry(addr, w.AmountInGwei, w.Index, w.ValidatorIndex));
            }
            return result;
        }
    }
}
