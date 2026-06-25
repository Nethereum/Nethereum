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
        // Frontier: base constants, txDataNonZero=68, NO extra txCreate gas (pre-EIP-2)
        public static readonly IntrinsicGasRules Frontier = new IntrinsicGasRules(
            txBase: 21000, txCreate: 0, txDataZero: 4, txDataNonZero: 68,
            initCode: null, accessList: null, blob: null, floor: null);

        // Homestead (EIP-2): added 32000 gas for contract creation transactions
        private static readonly IntrinsicGasRules _homestead = new IntrinsicGasRules(
            txBase: 21000, txCreate: 32000, txDataZero: 4, txDataNonZero: 68,
            initCode: null, accessList: null, blob: null, floor: null);

        // Istanbul onwards: EIP-2028 reduced txDataNonZero from 68 to 16
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

        // Berlin: EIP-2930 access list gas (on top of Istanbul's EIP-2028 base)
        public static readonly IntrinsicGasRules Berlin =
            PostEip2028.WithAccessList(Eip2930AccessListGasRule.Instance);

        public static readonly IntrinsicGasRules London = Berlin;
        public static readonly IntrinsicGasRules Paris = Berlin;

        // Shanghai: EIP-3860 initcode word gas
        public static readonly IntrinsicGasRules Shanghai =
            Berlin.WithInitCode(Eip3860InitCodeGasRule.Instance);

        // Cancun: EIP-4844 blob gas
        public static readonly IntrinsicGasRules Cancun = new IntrinsicGasRules(
            txBase: 21000,
            txCreate: 32000,
            txDataZero: 4,
            txDataNonZero: 16,
            initCode: Eip3860InitCodeGasRule.Instance,
            accessList: Eip2930AccessListGasRule.Instance,
            blob: Eip4844BlobGasRule.Instance,
            floor: null);

        /// <summary>
        /// Prague bundle: Cancun with the EIP-7623 calldata floor and the
        /// EIP-7691 blob throughput / fee-update-fraction bump. Built as
        /// <c>Cancun.WithBlob(Eip7691BlobGasRule.Instance).WithFloor(...)</c>
        /// so every unchanged slot (init code, access list) is preserved
        /// by reference.
        /// </summary>
        public static readonly IntrinsicGasRules Prague =
            Cancun.WithBlob(Eip7691BlobGasRule.Instance).WithFloor(Eip7623CalldataFloorRule.Instance);

        /// <summary>
        /// Osaka bundle: Prague intrinsic tx gas rules with the
        /// EIP-7892 blob-base-fee update fraction (8,346,193) replacing
        /// Prague's 5,007,716. Same target/max blob count as Prague
        /// (6 / 9) — only the rescheduling fraction changes.
        /// </summary>
        public static readonly IntrinsicGasRules Osaka =
            Prague.WithBlob(Eip7892BlobGasRule.Instance);

        /// <summary>
        /// First BPO (Blob Parameter Only) fork after Osaka activation,
        /// per EIP-7892. Identical to Osaka except for the blob-base-fee
        /// update fraction: 11,684,671 instead of 8,346,193.
        /// </summary>
        public static readonly IntrinsicGasRules OsakaBpo1 =
            Osaka.WithBlob(Eip7892Bpo1BlobGasRule.Instance);
    }
}
