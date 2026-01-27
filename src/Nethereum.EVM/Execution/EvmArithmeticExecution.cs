using System.Numerics;
using System.Runtime.CompilerServices;
using Nethereum.ABI;
using Nethereum.EVM;

namespace Nethereum.EVM.Execution
{

    public class EvmArithmeticExecution
    {
        public static BigInteger Two256 = IntType.MAX_UINT256_VALUE + 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SDiv(Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            if (second == 0)
            {
                program.StackPushSigned(0);
            }
            else if (second == -1 && first == -IntType.MAX_UINT256_VALUE)
            {
                program.StackPushSigned(-IntType.MAX_UINT256_VALUE);
            }
            else
            {
                var result = first / second;
                program.StackPushSigned(result);

            }
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SMod(Program program)
        {
            var first = program.StackPopAndConvertToBigInteger();
            var second = program.StackPopAndConvertToBigInteger();
            if (second == 0)
            {
                program.StackPushSigned(0);
            }
            else
            {
                var result = first % second;
                program.StackPushSigned(result);
            }
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddMod(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            var third = program.StackPopAndConvertToUBigInteger();
            if (third == 0)
            {
                var result = 0;
                program.StackPush(result);
            }
            else
            {
                var result = (first + second) % third;
                program.StackPush(result);
            }
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MulMod(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            var third = program.StackPopAndConvertToUBigInteger();
            if (third == 0)
            {
                var result = 0;
                program.StackPush(result);
            }
            else
            {
                var result = first * second % third;
                program.StackPush(result);
            }
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            var result = (first + second) % Two256;

            program.StackPush(result);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Exp(Program program)
        {
            var value = program.StackPopAndConvertToUBigInteger();
            var exponent = program.StackPopAndConvertToUBigInteger();
            var result = BigInteger.ModPow(value, exponent, Two256);
            program.StackPush(result);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Mul(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            var result = first * second % Two256;

            program.StackPush(result);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Sub(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            var result = (first - second) % Two256;
            program.StackPush(result);
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Div(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            if (second == 0)
            {
                program.StackPush(0);

            }
            else
            {
                var result = first / second;
                program.StackPush(result);
            }
            program.Step();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Mod(Program program)
        {
            var first = program.StackPopAndConvertToUBigInteger();
            var second = program.StackPopAndConvertToUBigInteger();
            if (second == 0)
            {
                program.StackPush(0);
            }
            else
            {
                var result = first % second;
                program.StackPush(result);
            }
            program.Step();
        }
    }
}