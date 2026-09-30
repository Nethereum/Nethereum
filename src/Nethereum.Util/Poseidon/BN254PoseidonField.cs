namespace Nethereum.Util.Poseidon
{
    public class BN254PoseidonField : IPoseidonFieldOps<BN254FieldElement>
    {
        public BN254FieldElement Zero => BN254FieldElement.Zero;

        public BN254FieldElement AddMod(BN254FieldElement a, BN254FieldElement b)
            => BN254FieldElement.Add(a, b);

        public BN254FieldElement MulMod(BN254FieldElement a, BN254FieldElement b)
            => BN254FieldElement.Multiply(a, b);

        public BN254FieldElement ModPow(BN254FieldElement baseVal, BN254FieldElement exponent)
        {
            // The only exponent used in Poseidon is 5 (S-box)
            if (exponent.L0 == 5 && exponent.L1 == 0 && exponent.L2 == 0 && exponent.L3 == 0)
                return BN254FieldElement.Pow5(baseVal);

            // Generic fallback — should not be hit in normal Poseidon operation
            return GenericModPow(baseVal, exponent);
        }

        public BN254FieldElement FromBytes(byte[] data)
            => BN254FieldElement.FromBytes(data);

        public byte[] ToBytes(BN254FieldElement value)
            => value.ToBytes();

        private static BN254FieldElement GenericModPow(BN254FieldElement baseVal, BN254FieldElement exp)
        {
            var result = BN254FieldElement.One;
            while (exp.L0 != 0 || exp.L1 != 0 || exp.L2 != 0 || exp.L3 != 0)
            {
                if ((exp.L0 & 1) == 1)
                    result = BN254FieldElement.Multiply(result, baseVal);
                baseVal = BN254FieldElement.Multiply(baseVal, baseVal);
                // Shift right by 1
                ulong l0 = (exp.L0 >> 1) | (exp.L1 << 63);
                ulong l1 = (exp.L1 >> 1) | (exp.L2 << 63);
                ulong l2 = (exp.L2 >> 1) | (exp.L3 << 63);
                ulong l3 = exp.L3 >> 1;
                exp = new BN254FieldElement(l0, l1, l2, l3);
            }
            return result;
        }
    }
}
