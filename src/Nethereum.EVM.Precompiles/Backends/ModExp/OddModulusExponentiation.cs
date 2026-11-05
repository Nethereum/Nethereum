#if NET5_0_OR_GREATER && !MODEXP_PORTABLE
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nethereum.EVM.Precompiles.Backends.ModExp
{
    internal sealed class OddModulusExponentiation
    {
        private const int BinaryExponentBitLimit = 64;
        private const int WindowBits = 4;

        private readonly ulong[] _modulus;
        private readonly int _length;
        private readonly ulong _negatedInverse;
        private readonly ulong[] _scratch;

        private OddModulusExponentiation(ulong[] modulus)
        {
            _modulus = modulus;
            _length = modulus.Length;
            _negatedInverse = NegatedInverse(modulus[0]);
            _scratch = new ulong[_length + 1];
        }

        public static ulong[] Power(byte[] baseBytes, ModExpExponent exponent, ulong[] modulus)
        {
            var context = new OddModulusExponentiation(modulus);
            var reducedBase = context.Reduce(baseBytes);
            if (IsZero(reducedBase)) return new ulong[0];
            if (ModExpLimbs.IsOne(ModExpLimbs.Trim(reducedBase))) return new ulong[] { 1 };
            return context.Power(reducedBase, exponent);
        }

        private static bool IsZero(ulong[] limbs)
        {
            foreach (var limb in limbs)
            {
                if (limb != 0) return false;
            }
            return true;
        }

        private ulong[] Power(ulong[] reducedBase, ModExpExponent exponent)
        {
            var rSquared = RSquared();
            var one = ModExpLimbs.Resize(new ulong[] { 1 }, _length);
            int width = exponent.BitLength <= BinaryExponentBitLimit ? 1 : WindowBits;
            var table = PowerTable(Multiply(reducedBase, rSquared), width);
            int digits = (exponent.BitLength + width - 1) / width;
            var accumulator = (ulong[])table[exponent.Window((digits - 1) * width, width)].Clone();
            for (int digit = digits - 2; digit >= 0; digit--)
            {
                for (int i = 0; i < width; i++) MultiplyInto(accumulator, accumulator, accumulator);
                int selected = exponent.Window(digit * width, width);
                if (selected != 0) MultiplyInto(accumulator, table[selected], accumulator);
            }
            return ModExpLimbs.Trim(Multiply(accumulator, one));
        }

        private ulong[][] PowerTable(ulong[] montgomeryBase, int width)
        {
            var table = new ulong[1 << width][];
            table[1] = montgomeryBase;
            for (int i = 2; i < table.Length; i++) table[i] = Multiply(table[i - 1], montgomeryBase);
            return table;
        }

        private ulong[] Reduce(byte[] baseBytes)
        {
            var limbs = ModExpLimbs.FromBigEndian(baseBytes);
            bool reduced = limbs.Length < _length
                || (limbs.Length == _length && ModExpLimbs.Compare(limbs, _modulus, _length) < 0);
            if (reduced) return ModExpLimbs.Resize(limbs, _length);
            var remainder = ModExpLimbs.ToBigInteger(limbs) % ModExpLimbs.ToBigInteger(_modulus);
            return ModExpLimbs.Resize(ModExpLimbs.FromBigInteger(remainder), _length);
        }

        private ulong[] RSquared()
        {
            int topBit = (_length - 1) * 64 + 63 - BitOperations.LeadingZeroCount(_modulus[_length - 1]);
            var oneInMontgomeryForm = new ulong[_length];
            oneInMontgomeryForm[topBit >> 6] = 1UL << (topBit & 63);
            for (int i = topBit; i < _length * 64; i++) Double(oneInMontgomeryForm);
            var power = (ulong[])oneInMontgomeryForm.Clone();
            Double(power);
            int target = _length * 64;
            for (int bit = 30 - BitOperations.LeadingZeroCount((uint)target); bit >= 0; bit--)
            {
                MultiplyInto(power, power, power);
                if (((target >> bit) & 1) != 0) Double(power);
            }
            return power;
        }

        private void Double(ulong[] value)
        {
            ulong carry = 0;
            for (int i = 0; i < _length; i++)
            {
                ulong next = value[i] >> 63;
                value[i] = (value[i] << 1) | carry;
                carry = next;
            }
            if (carry != 0 || ModExpLimbs.Compare(value, _modulus, _length) >= 0) SubtractModulus(value);
        }

        private ulong[] Multiply(ulong[] a, ulong[] b)
        {
            var result = new ulong[_length];
            MultiplyInto(a, b, result);
            return result;
        }

        private void MultiplyInto(ulong[] a, ulong[] b, ulong[] result)
        {
            int n = _length;
            ref ulong t = ref MemoryMarshal.GetArrayDataReference(_scratch);
            ref ulong m = ref MemoryMarshal.GetArrayDataReference(_modulus);
            ref ulong bRef = ref MemoryMarshal.GetArrayDataReference(b);
            for (int j = 0; j <= n; j++) Unsafe.Add(ref t, j) = 0;
            for (int i = 0; i < n; i++)
            {
                ulong ai = a[i];
                ulong productCarry = MulAddAdd(ai, bRef, t, 0, out ulong first);
                ulong q = first * _negatedInverse;
                ulong reductionCarry = MulAddAdd(q, m, first, 0, out _);
                for (int j = 1; j < n; j++)
                {
                    productCarry = MulAddAdd(ai, Unsafe.Add(ref bRef, j), Unsafe.Add(ref t, j), productCarry, out ulong low);
                    reductionCarry = MulAddAdd(q, Unsafe.Add(ref m, j), low, reductionCarry, out Unsafe.Add(ref t, j - 1));
                }
                ulong sum = Unsafe.Add(ref t, n) + productCarry;
                ulong overflow = sum < productCarry ? 1UL : 0UL;
                sum += reductionCarry;
                overflow += sum < reductionCarry ? 1UL : 0UL;
                Unsafe.Add(ref t, n - 1) = sum;
                Unsafe.Add(ref t, n) = overflow;
            }
            if (_scratch[n] != 0 || ModExpLimbs.Compare(_scratch, _modulus, n) >= 0) SubtractModulus(_scratch);
            Array.Copy(_scratch, result, n);
        }

        private void SubtractModulus(ulong[] t)
        {
            ulong borrow = 0;
            for (int i = 0; i < _length; i++)
            {
                ulong minuend = t[i];
                ulong subtrahend = _modulus[i];
                ulong difference = minuend - subtrahend - borrow;
                borrow = minuend < subtrahend || (minuend == subtrahend && borrow != 0) ? 1UL : 0UL;
                t[i] = difference;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong MulAddAdd(ulong x, ulong y, ulong addend1, ulong addend2, out ulong low)
        {
            ulong high = Math.BigMul(x, y, out ulong product);
            product += addend1;
            high += product < addend1 ? 1UL : 0UL;
            product += addend2;
            high += product < addend2 ? 1UL : 0UL;
            low = product;
            return high;
        }

        private static ulong NegatedInverse(ulong lowLimb)
        {
            ulong inverse = lowLimb;
            for (int i = 0; i < 5; i++) inverse *= 2 - lowLimb * inverse;
            return 0 - inverse;
        }
    }
}

#endif
