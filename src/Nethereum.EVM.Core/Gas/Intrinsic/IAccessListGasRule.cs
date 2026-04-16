using System.Collections.Generic;

namespace Nethereum.EVM.Gas.Intrinsic
{
    /// <summary>
    /// Per-fork rule for the EIP-2930 transaction access list gas
    /// surcharge. Installed on <see cref="IntrinsicGasRules"/> from
    /// Berlin onwards; null on a bundle means "access list gas is not
    /// active at this fork" and the surcharge is skipped (matching
    /// pre-Berlin semantics where type-1 transactions did not exist).
    /// </summary>
    public interface IAccessListGasRule
    {
        /// <summary>
        /// Returns the total access list gas cost for
        /// <paramref name="accessList"/>. Implementations must return
        /// 0 for a null or empty list.
        /// </summary>
        long CalculateGas(IList<AccessListEntry> accessList);
    }
}
