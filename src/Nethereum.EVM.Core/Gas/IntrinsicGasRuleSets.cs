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
        /// Prague bundle: Cancun with the EIP-7623 calldata floor
        /// installed. Built as <c>Cancun.WithFloor(...)</c> so every
        /// unchanged slot (init code, access list, blob) is preserved
        /// by reference.
        /// </summary>
        public static readonly IntrinsicGasRules Prague =
            Cancun.WithFloor(Eip7623CalldataFloorRule.Instance);

        /// <summary>
        /// Osaka bundle: no intrinsic tx gas changes relative to
        /// Prague, so it is the same reference. Kept as a named
        /// property for symmetry with <see cref="Cancun"/> and
        /// <see cref="Prague"/>, and so downstream fork additions
        /// only have to call <c>.WithXxx(...)</c> on Osaka rather
        /// than reach through Prague.
        /// </summary>
        public static readonly IntrinsicGasRules Osaka = Prague;
    }
}
