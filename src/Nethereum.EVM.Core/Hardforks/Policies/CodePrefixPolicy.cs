namespace Nethereum.EVM.Hardforks.Policies
{
    /// <summary>
    /// EIP-3541 reserved code prefix policy. Decides whether the
    /// CREATE/CREATE2 deployed code is rejected when its first byte is
    /// <c>0xEF</c>. The 0xEF prefix is reserved for future EOF
    /// (EIP-3540 / EIP-3670) container formats.
    ///
    /// <para><b>Fork history:</b></para>
    /// <list type="bullet">
    ///   <item>Frontier through Berlin — <see cref="Permissive"/>. Code
    ///   starting with 0xEF is accepted as ordinary bytecode (likely
    ///   junk but executable).</item>
    ///   <item>London (EIP-3541) onwards — <see cref="Eip3541RejectEf"/>.
    ///   Deployed code starting with 0xEF causes the CREATE/CREATE2 to
    ///   revert with all gas consumed. Existing contracts with 0xEF code
    ///   (already on-chain) continue to function — only new deploys are
    ///   blocked.</item>
    /// </list>
    ///
    /// <para><b>Geth ref:</b> <c>core/vm/evm.go create</c> (search for
    /// <c>HasEOFByte</c>) → <c>core/vm/eips.go enable3541</c>.</para>
    /// </summary>
    public abstract class CodePrefixPolicy
    {
        /// <summary>Pre-EIP-3541: 0xEF prefix accepted.</summary>
        public static readonly CodePrefixPolicy Permissive = new PermissivePolicy();

        /// <summary>EIP-3541 (London+): 0xEF prefix rejected at deploy.</summary>
        public static readonly CodePrefixPolicy Eip3541RejectEf = new Eip3541RejectEfPolicy();

        /// <summary>
        /// True when CREATE/CREATE2 must reject code whose first byte
        /// is 0xEF.
        /// </summary>
        public abstract bool RejectsEfPrefix { get; }

        private sealed class PermissivePolicy : CodePrefixPolicy
        {
            public override bool RejectsEfPrefix => false;
        }

        private sealed class Eip3541RejectEfPolicy : CodePrefixPolicy
        {
            public override bool RejectsEfPrefix => true;
        }
    }
}
