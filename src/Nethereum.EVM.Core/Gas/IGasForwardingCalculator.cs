namespace Nethereum.EVM.Gas
{
    public interface IGasForwardingCalculator
    {
        /// <summary>
        /// Max gas the calling frame can hand off to a sub-frame whose gas
        /// amount is NOT under user control (CREATE/CREATE2 — geth's
        /// gasCreate forwards the parent's remaining gas directly).
        /// </summary>
        long CalculateMaxGasToForward(long gasRemaining);

        /// <summary>
        /// Gas to allocate to a CALL / CALLCODE / DELEGATECALL / STATICCALL
        /// sub-frame when the user has pushed a desired gas amount on the
        /// stack. Mirrors go-ethereum's <c>callGas</c> in
        /// <c>core/vm/gas.go</c>:
        /// <para>
        ///   pre-EIP-150 returns <paramref name="userRequestedGas"/> as-is
        ///   (no cap; if the static base + user-requested gas exceeds the
        ///   caller's remaining gas the CALL op OOGs and consumes all gas);
        /// </para>
        /// <para>
        ///   EIP-150+ caps at <c>(gasRemaining) - (gasRemaining)/64</c> and
        ///   returns <c>min(userRequestedGas, cap)</c>.
        /// </para>
        /// The static base cost (access + value-transfer + memory + new-
        /// account) has already been deducted from <paramref name="gasRemaining"/>
        /// by the time this is called.
        /// </summary>
        long CalculateGasForCall(long gasRemaining, long userRequestedGas);
    }

    public sealed class Eip150GasForwarding : IGasForwardingCalculator
    {
        public static readonly Eip150GasForwarding Instance = new Eip150GasForwarding();

        public long CalculateMaxGasToForward(long gasRemaining)
        {
            return gasRemaining - (gasRemaining / 64);
        }

        public long CalculateGasForCall(long gasRemaining, long userRequestedGas)
        {
            var cap = gasRemaining - (gasRemaining / 64);
            return userRequestedGas < cap ? userRequestedGas : cap;
        }
    }

    public sealed class FullGasForwarding : IGasForwardingCalculator
    {
        public static readonly FullGasForwarding Instance = new FullGasForwarding();

        public long CalculateMaxGasToForward(long gasRemaining)
        {
            return gasRemaining;
        }

        /// <summary>
        /// Pre-EIP-150: forwards exactly what the user asked for. May exceed
        /// <paramref name="gasRemaining"/> — the caller must treat that as
        /// OutOfGas and consume the remaining gas.
        /// </summary>
        public long CalculateGasForCall(long gasRemaining, long userRequestedGas)
        {
            return userRequestedGas;
        }
    }
}
