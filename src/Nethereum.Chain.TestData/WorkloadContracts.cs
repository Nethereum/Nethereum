using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Chain.TestData
{
    public static class WorkloadContracts
    {
        public const string StorageLoggerHex = "0x602a60005560006000a0600060005360016000f3";

        public static byte[] StorageLoggerBytecode => StorageLoggerHex.HexToByteArray();

        public const string CallableStorageLoggerHex = "0x600b600c600039600b6000f3606360015560006000a000";

        public static byte[] CallableStorageLoggerBytecode => CallableStorageLoggerHex.HexToByteArray();

        public const string RevertOnCallHex = "0x6005600c60003960056000f360006000fd";

        public static byte[] RevertOnCallBytecode => RevertOnCallHex.HexToByteArray();

        // CREATE bytecode whose CONSTRUCTOR writes storage then SELFDESTRUCTs (beneficiary 0x..01).
        // Created and destroyed in the same tx, so under EIP-6780 the account is deleted and its
        // storage cleared — exercises the storage-clear / dirty-account path.
        //   602a600055                     SSTORE(0, 0x2a)
        //   73<20-byte addr> ff            PUSH20 beneficiary ; SELFDESTRUCT
        public const string SelfDestructInConstructorHex = "0x602a600055730000000000000000000000000000000000000001ff";

        public static byte[] SelfDestructInConstructorBytecode => SelfDestructInConstructorHex.HexToByteArray();

        public const string MultiStorageLoggerHex =
            "0x60116000556022600155603360025560aa60006000a160cc60bb60006000a2600060005360016000f3";

        public static byte[] MultiStorageLoggerBytecode => MultiStorageLoggerHex.HexToByteArray();

        public const string ForwarderHex =
            "0x6013600c60003960136000f3600060006000600060006000355af160005500";

        public static byte[] ForwarderBytecode => ForwarderHex.HexToByteArray();

        public const string VaultHex = "0x600060005360016000f3";
        public static byte[] VaultBytecode => VaultHex.HexToByteArray();

        public const string DelegateForwarderHex =
            "0x6011600c60003960116000f360006000600060006000355af460005500";

        public static byte[] DelegateForwarderBytecode => DelegateForwarderHex.HexToByteArray();

        // CREATE bytecode whose CONSTRUCTOR writes slot 0 = 0x2a, then installs a RUNTIME that on every CALL
        // does SSTORE(0, SLOAD(0) + 1) — a READ-MODIFY-WRITE of an existing storage slot (the shape of an
        // ERC20 balanceOf update). Deploy + repeated calls walk slot 0 through 42, 43, 44…
        //   ctor:    602a600055                  SSTORE(0, 0x2a)
        //            600a6011600039 600a6000f3   CODECOPY 10-byte runtime ; RETURN it
        //   runtime: 6000 54 6001 01 6000 55 00  PUSH1 0 SLOAD PUSH1 1 ADD PUSH1 0 SSTORE STOP
        public const string IncrementOnCallHex = "0x602a600055600a6011600039600a6000f360005460010160005500";

        public static byte[] IncrementOnCallBytecode => IncrementOnCallHex.HexToByteArray();

        public const string LargeStorageHex =
            "0x6104b060005b81811015610019576001815560010161000556" +
            "5b600060005360016000f3";

        public static byte[] LargeStorageBytecode => LargeStorageHex.HexToByteArray();
    }
}
