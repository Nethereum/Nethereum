using System.Runtime.CompilerServices;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    public class EvmBitwiseExecution
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ShiftSignedRight(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToBigInteger();

            if (first >= 256)
            {
                if (second < 0)
                {
                    program.StackPushSigned(-1);
                }
                else
                {
                    program.StackPushSigned(0);
                }
            }
            else
            {
                program.StackPush(second >> (int)first);
            }

            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SignExtend(Program program)
        {
            var widthBig = program.StackPopAndConvertToUBigInteger();
            var item = program.StackPop();

            if (widthBig < 32)
            {
                var width = (int)widthBig;
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
            var pos = program.StackPopAndConvertToUBigInteger();
            var byteBytes = program.StackPop();
            var word = byteBytes.PadTo32Bytes();

            var result = pos < 32 ? new[] { word[(int)pos] } : new byte[1] { 0 };
            program.StackPush(result);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void And(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            program.StackPush(first & second);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ShiftLeft(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPop();
            if (first >= 256)
            {
                program.StackPush(0);
            }
            else
            {
                program.StackPush(second.ShiftLeft((int)first));
            }
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ShiftRight(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            if (first >= 256)
            {
                program.StackPush(0);
            }
            else
            {
                program.StackPush(second >> (int)first);
            }
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OrSimple(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            program.StackPush(first | second);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Or(Program program)
        {
            var first = program.StackPop();

            var second = program.StackPop();

            var convertedValue = new byte[second.Length];
            for (int i = 0; i < second.Length; i++)
                convertedValue[i] = (byte)(second[i] | first[i]);
            program.StackPush(convertedValue.PadTo32Bytes());
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void XorSimple(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            program.StackPush(first ^ second);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Xor(Program program)
        {
            var first = program.StackPop();
            var second = program.StackPop();
            var convertedValue = new byte[second.Length];
            for (int i = 0; i < second.Length; i++)
                convertedValue[i] = (byte)(second[i] ^ first[i]);
            program.StackPush(convertedValue.PadTo32Bytes());
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Not(Program program)
        {
            var value = program.StackPopAndConvertToUBigInteger();
            program.StackPush(~value);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LT(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            program.StackPush(first < second ? 1 : 0);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EQ(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            program.StackPush(first == second ? 1 : 0);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IsZero(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            program.StackPush(first == 0 ? 1 : 0);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GT(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            program.StackPush(first > second ? 1 : 0);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SLT(Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            program.StackPush(first < second ? 1 : 0);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SGT(Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            program.StackPush(first > second ? 1 : 0);
            program.Step();
        }
    }
}