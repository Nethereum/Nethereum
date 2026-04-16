using System.Runtime.CompilerServices;
using Nethereum.Util;
using Nethereum.EVM;

namespace Nethereum.EVM.Execution
{

    public class EvmArithmeticExecution
    {
        private static readonly EvmUInt256 INT256_MIN = new EvmUInt256(0x8000000000000000, 0, 0, 0);
        private static readonly EvmUInt256 UINT256_NEG_ONE = EvmUInt256.MaxValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SDiv(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            if (second.IsZero)
            {
                program.StackPush(EvmUInt256.Zero);
            }
            else if (second == UINT256_NEG_ONE && first == INT256_MIN)
            {
                program.StackPush(INT256_MIN);
            }
            else
            {
                bool firstNeg = first.IsHighBitSet;
                bool secondNeg = second.IsHighBitSet;
                var absFirst = firstNeg ? first.Negate() : first;
                var absSecond = secondNeg ? second.Negate() : second;
                var result = absFirst / absSecond;
                if (firstNeg != secondNeg)
                    result = result.Negate();
                program.StackPush(result);
            }
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SMod(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            if (second.IsZero)
            {
                program.StackPush(EvmUInt256.Zero);
            }
            else
            {
                bool firstNeg = first.IsHighBitSet;
                bool secondNeg = second.IsHighBitSet;
                var absFirst = firstNeg ? first.Negate() : first;
                var absSecond = secondNeg ? second.Negate() : second;
                var result = absFirst % absSecond;
                if (firstNeg)
                    result = result.Negate();
                program.StackPush(result);
            }
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddMod(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            var third = program.StackPopU256();
            if (third.IsZero)
            {
                program.StackPush(EvmUInt256.Zero);
            }
            else
            {
                program.StackPush(EvmUInt256.AddMod(first, second, third));
            }
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MulMod(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            var third = program.StackPopU256();
            if (third.IsZero)
            {
                program.StackPush(EvmUInt256.Zero);
            }
            else
            {
                program.StackPush(EvmUInt256.MulMod(first, second, third));
            }
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first + second);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Exp(Program program)
        {
            var value = program.StackPopU256();
            var exponent = program.StackPopU256();
            // 2^256 as modulus is handled by EvmUInt256 wrapping — ModPow with MaxValue+1
            // Since EvmUInt256 naturally wraps at 256 bits, we can use a simpler approach
            var result = EvmUInt256.One;
            var b = value;
            var e = exponent;
            while (!e.IsZero)
            {
                if ((e.U0 & 1) == 1)
                    result = result * b;
                b = b * b;
                e = e >> 1;
            }
            program.StackPush(result);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Mul(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first * second);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Sub(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first - second);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Div(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first / second);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Mod(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first % second);
            program.Step();
        }
    }
}
