using Nethereum.Util;

namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    /// <summary>
    /// Cross-cutting rule <b>R3</b> — EIP-2929 cold/warm storage key
    /// access accounting. Used by <c>SLOAD</c> and <c>SSTORE</c>.
    /// Returns the access cost and marks the key warm as a side-effect
    /// on first touch — same atomic semantics as
    /// <see cref="IAccessAccountRule"/> but for storage slots.
    /// </summary>
    public interface IAccessStorageRule
    {
        /// <summary>
        /// Returns the EIP-2929 storage-key access cost and marks the
        /// key warm if it wasn't already.
        /// </summary>
        long GetAccessCost(Program program, EvmUInt256 key);
    }
}
