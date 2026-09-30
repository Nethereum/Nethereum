using System.Collections.Generic;

namespace Nethereum.EVM.Gas.Intrinsic
{
    /// <summary>
    /// Per-fork rule for the EIP-7981 access-list data surcharge's
    /// contribution to the calldata floor. Installed on
    /// <see cref="IntrinsicGasRules"/> from Amsterdam onwards; null on a
    /// bundle means "no access-list floor surcharge at this fork" (Osaka
    /// and earlier), matching <see cref="ICalldataFloorRule"/>'s null
    /// convention.
    ///
    /// <para>
    /// Deliberately a SEPARATE interface from <see cref="ICalldataFloorRule"/>,
    /// not an overload of it: the floor's calldata-byte token count and its
    /// access-list token count are two different quantities from two
    /// different inputs (bytes vs. entries), the same reasoning that kept
    /// <see cref="ICalldataFloorRule.FloorTokensInCalldata"/> distinct from
    /// the standard per-byte token count.
    /// </para>
    /// </summary>
    public interface IAccessListFloorRule
    {
        long FloorTokensInAccessList(IList<AccessListEntry> accessList);

        long FloorPerTokenGas(IList<AccessListEntry> accessList);
    }
}
