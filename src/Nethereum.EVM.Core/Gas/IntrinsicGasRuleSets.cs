using Nethereum.EVM.Gas.Intrinsic;

namespace Nethereum.EVM.Gas
{
    /// <summary>
    /// Per-fork intrinsic tx gas bundles. Each fork is a fresh
    /// <see cref="IntrinsicGasRules"/> built by composition from the
    /// previous fork's bundle — no class inheritance. Reading a bundle
    /// top-to-bottom tells you exactly what changed relative to its
    /// parent: <see cref="Prague"/> adds the EIP-7623 calldata floor
    /// on top of Cancun; <see cref="Osaka"/> has no intrinsic changes
    /// versus Prague so it is the same reference.
    ///
    /// Consumers that want a custom fork shape (e.g. "Cancun + EIP-7623
    /// without the blob rule") compose freely via the
    /// <c>.WithXxx(...)</c> setters on <see cref="IntrinsicGasRules"/>.
    /// </summary>
    public static class IntrinsicGasRuleSets
    {
        public static readonly IntrinsicGasRules Frontier = new IntrinsicGasRules(
            txBase: 21000, txCreate: 0, txDataZero: 4, txDataNonZero: 68,
            initCode: null, accessList: null, blob: null, floor: null);

        private static readonly IntrinsicGasRules _homestead = new IntrinsicGasRules(
            txBase: 21000, txCreate: 32000, txDataZero: 4, txDataNonZero: 68,
            initCode: null, accessList: null, blob: null, floor: null);

        public static readonly IntrinsicGasRules PostEip2028 = new IntrinsicGasRules(
            txBase: 21000, txCreate: 32000, txDataZero: 4, txDataNonZero: 16,
            initCode: null, accessList: null, blob: null, floor: null);

        public static readonly IntrinsicGasRules Homestead = _homestead;
        public static readonly IntrinsicGasRules TangerineWhistle = _homestead;
        public static readonly IntrinsicGasRules SpuriousDragon = _homestead;
        public static readonly IntrinsicGasRules Byzantium = _homestead;
        public static readonly IntrinsicGasRules Constantinople = _homestead;
        public static readonly IntrinsicGasRules Petersburg = _homestead;
        public static readonly IntrinsicGasRules Istanbul = PostEip2028;

        public static readonly IntrinsicGasRules Berlin =
            PostEip2028.WithAccessList(Eip2930AccessListGasRule.Instance);

        public static readonly IntrinsicGasRules London = Berlin;
        public static readonly IntrinsicGasRules Paris = Berlin;

        public static readonly IntrinsicGasRules Shanghai =
            Berlin.WithInitCode(Eip3860InitCodeGasRule.Instance);

        public static readonly IntrinsicGasRules Cancun = new IntrinsicGasRules(
            txBase: 21000,
            txCreate: 32000,
            txDataZero: 4,
            txDataNonZero: 16,
            initCode: Eip3860InitCodeGasRule.Instance,
            accessList: Eip2930AccessListGasRule.Instance,
            blob: Eip4844BlobGasRule.Instance,
            floor: null);

        public static readonly IntrinsicGasRules Prague =
            Cancun.WithBlob(Eip7691BlobGasRule.Instance).WithFloor(Eip7623CalldataFloorRule.Instance);

        public static readonly IntrinsicGasRules Osaka =
            Prague.WithBlob(Eip7892BlobGasRule.Instance);

        public static readonly IntrinsicGasRules OsakaBpo1 =
            Osaka.WithBlob(Eip7892Bpo1BlobGasRule.Instance);

        public static readonly IntrinsicGasRules OsakaBpo2 =
            OsakaBpo1.WithBlob(Eip7892Bpo2BlobGasRule.Instance);

        public static readonly IntrinsicGasRules Amsterdam = new IntrinsicGasRules(
            txBase: GasConstants.EIP2780_TX_BASE_COST,
            txCreate: 0,
            txDataZero: OsakaBpo2.TxDataZero,
            txDataNonZero: OsakaBpo2.TxDataNonZero,
            initCode: OsakaBpo2.InitCode,
            accessList: Eip7981AccessListGasRule.Instance,
            blob: AmsterdamBlobGasRule.Instance,
            floor: Eip7976CalldataFloorRule.Instance,
            recipient: Eip2780RecipientGasRule.Instance,
            accessListFloor: Eip7981AccessListGasRule.Instance);
    }
}
