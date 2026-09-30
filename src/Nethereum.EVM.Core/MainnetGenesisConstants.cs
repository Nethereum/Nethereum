namespace Nethereum.EVM
{
    public static class MainnetGenesisConstants
    {
        public const long ChainId = 1;

        public const string StateRootHex =
            "0xd7f8974fb5ac78d9ac099b9ad5018bedc2ce0a72dad1827a1709da30580f0544";

        public const string BlockHashHex =
            "0xd4e56740f876aef8c010b86a40d5f56745a118d0906a34e69aec8c0db1cb8fa3";

        public const ulong Difficulty = 17_179_869_184UL;

        public const long GasLimit = 5000;

        public const ulong Timestamp = 0;

        public const string NonceHex = "0x0000000000000042";

        public const string MixHashHex =
            "0x0000000000000000000000000000000000000000000000000000000000000000";

        public const string CoinbaseHex = "0x0000000000000000000000000000000000000000";

        public const string ExtraDataHex =
            "0x11bbe8db4e347b4e8c937c1c8370e4b5ed33adb3db69cbdb7a38e1e50b1b82fa";

        public const int AllocAccountCount = 8893;

        public const string TotalAllocBalanceWei = "72009990499480000000000000";
    }
}
