namespace Nethereum.Signer.Crypto.BN128.Managed
{
    internal readonly struct Limbs256
    {
        public readonly ulong L0;
        public readonly ulong L1;
        public readonly ulong L2;
        public readonly ulong L3;

        public Limbs256(ulong l0, ulong l1, ulong l2, ulong l3)
        {
            L0 = l0;
            L1 = l1;
            L2 = l2;
            L3 = l3;
        }
    }
}
