using System.Runtime.CompilerServices;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    public class EvmBitwiseExecution
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ShiftSignedRight(Program program)
        {
            var shift = program.StackPopU256();
            var value = program.StackPopU256();

            if (shift >= new EvmUInt256(256))
            {
                program.StackPush(value.IsHighBitSet ? EvmUInt256.MaxValue : EvmUInt256.Zero);
            }
            else
            {
                program.StackPush(value.ArithmeticRightShift((int)(ulong)shift));
            }
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CountLeadingZeros(Program program)
        {
            var value = program.StackPopU256();
            program.StackPush(new EvmUInt256((ulong)EvmUInt256.LeadingZeroCount(value)));
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SignExtend(Program program)
        {
            var widthBig = program.StackPopU256();
            var item = program.StackPop();

            if (widthBig < new EvmUInt256(32))
            {
                var width = (int)(ulong)widthBig;
                var signedNegative = (item[31 - width] & 0x80) == 0x80;
                for (var i = 0; i < 31 - width; i++)
                    if (signedNegative)
                        item[i] = 0xFF;
                    else
                        item[i] = 0;
            }
            program.StackPush(item);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Byte(Program program)
        {
            var pos = program.StackPopU256();
            var value = program.StackPopU256();

            if (pos < new EvmUInt256(32))
            {
                program.StackPush(new EvmUInt256(value.GetByte((int)(ulong)pos)));
            }
            else
            {
                program.StackPush(EvmUInt256.Zero);
            }
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void And(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first & second);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ShiftLeft(Program program)
        {
            var shift = program.StackPopU256();
            var value = program.StackPopU256();
            if (shift >= new EvmUInt256(256))
            {
                program.StackPush(EvmUInt256.Zero);
            }
            else
            {
                program.StackPush(value << (int)(ulong)shift);
            }
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ShiftRight(Program program)
        {
            var shift = program.StackPopU256();
            var value = program.StackPopU256();
            if (shift >= new EvmUInt256(256))
            {
                program.StackPush(EvmUInt256.Zero);
            }
            else
            {
                program.StackPush(value >> (int)(ulong)shift);
            }
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OrSimple(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first | second);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Or(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first | second);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void XorSimple(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first ^ second);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Xor(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first ^ second);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Not(Program program)
        {
            var value = program.StackPopU256();
            program.StackPush(~value);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LT(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first < second ? EvmUInt256.One : EvmUInt256.Zero);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EQ(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first == second ? EvmUInt256.One : EvmUInt256.Zero);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IsZero(Program program)
        {
            var first = program.StackPopU256();
            program.StackPush(first.IsZero ? EvmUInt256.One : EvmUInt256.Zero);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GT(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first > second ? EvmUInt256.One : EvmUInt256.Zero);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SLT(Program program)
        {
            // Signed comparison via two's complement
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            bool firstNeg = first.IsHighBitSet;
            bool secondNeg = second.IsHighBitSet;

            bool result;
            if (firstNeg && !secondNeg) result = true;
            else if (!firstNeg && secondNeg) result = false;
            else result = first < second; // same sign: unsigned comparison works

            program.StackPush(result ? EvmUInt256.One : EvmUInt256.Zero);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SGT(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            bool firstNeg = first.IsHighBitSet;
            bool secondNeg = second.IsHighBitSet;

            bool result;
            if (firstNeg && !secondNeg) result = false;
            else if (!firstNeg && secondNeg) result = true;
            else result = first > second;

            program.StackPush(result ? EvmUInt256.One : EvmUInt256.Zero);
            program.Step();
        }
    }
}
