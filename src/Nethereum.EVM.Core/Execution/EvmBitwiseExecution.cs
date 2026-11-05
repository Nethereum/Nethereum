using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    public class EvmBitwiseExecution
    {
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

        public void CountLeadingZeros(Program program)
        {
            var value = program.StackPopU256();
            program.StackPush(new EvmUInt256((ulong)EvmUInt256.LeadingZeroCount(value)));
            program.Step();
        }

        public void SignExtend(Program program)
        {
            var width = program.StackPopU256();
            var value = program.StackPopU256();

            if (width < new EvmUInt256(31))
            {
                var signBit = (int)(ulong)width * 8 + 7;
                var lowBits = (EvmUInt256.One << (signBit + 1)) - EvmUInt256.One;
                var negative = !((value >> signBit) & EvmUInt256.One).IsZero;
                program.StackPush(negative ? value | ~lowBits : value & lowBits);
            }
            else
            {
                program.StackPush(value);
            }
            program.Step();
        }

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

        public void And(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first & second);
            program.Step();
        }

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

        public void OrSimple(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first | second);
            program.Step();
        }

        public void Or(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first | second);
            program.Step();
        }

        public void XorSimple(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first ^ second);
            program.Step();
        }

        public void Xor(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first ^ second);
            program.Step();
        }

        public void Not(Program program)
        {
            var value = program.StackPopU256();
            program.StackPush(~value);
            program.Step();
        }

        public void LT(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first < second ? EvmUInt256.One : EvmUInt256.Zero);
            program.Step();
        }

        public void EQ(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first == second ? EvmUInt256.One : EvmUInt256.Zero);
            program.Step();
        }

        public void IsZero(Program program)
        {
            var first = program.StackPopU256();
            program.StackPush(first.IsZero ? EvmUInt256.One : EvmUInt256.Zero);
            program.Step();
        }

        public void GT(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            program.StackPush(first > second ? EvmUInt256.One : EvmUInt256.Zero);
            program.Step();
        }

        public void SLT(Program program)
        {
            var first = program.StackPopU256();
            var second = program.StackPopU256();
            bool firstNeg = first.IsHighBitSet;
            bool secondNeg = second.IsHighBitSet;

            bool result;
            if (firstNeg && !secondNeg) result = true;
            else if (!firstNeg && secondNeg) result = false;
            else result = first < second;

            program.StackPush(result ? EvmUInt256.One : EvmUInt256.Zero);
            program.Step();
        }

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
