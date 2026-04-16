using System.Collections.Generic;

namespace Nethereum.EVM.Gas
{
    /// <summary>
    /// One entry in an EIP-2930 transaction access list: a pre-declared
    /// address plus any storage keys the caller intends to touch at that
    /// address. Used as input to the intrinsic gas calculation (each entry
    /// costs <c>2400 + 1900 × storageKeys.Count</c> under EIP-2930) and
    /// for pre-warming the accessed-addresses / accessed-storage sets at
    /// transaction start.
    ///
    /// Extracted from the legacy <c>IntrinsicGasCalculator.cs</c> into its
    /// own file so the legacy static class can be deleted
    /// without moving a dependent type.
    /// </summary>
    public class AccessListEntry
    {
        public string Address { get; set; }
        public IList<string> StorageKeys { get; set; }
    }
}
