namespace Nethereum.EVM.Execution.TransactionValidation.Rules
{
    /// <summary>
    /// Rejects EIP-1559 type-2 (dynamic fee) transactions before London and
    /// EIP-2930 type-1 (access list) transactions before Berlin. Geth's
    /// transaction validation returns TR_TypeNotSupported (or equivalent) for
    /// unsupported tx types per the active chainRules; without this rule our
    /// executor silently accepts the tx and diverges from the canonical
    /// "reject + zero gas charged" state.
    /// </summary>
    public sealed class PreLondonTxTypeRule : ITransactionValidationRule
    {
        private readonly bool _rejectAccessList;
        private readonly bool _rejectDynamicFee;

        public PreLondonTxTypeRule(bool rejectAccessList, bool rejectDynamicFee)
        {
            _rejectAccessList = rejectAccessList;
            _rejectDynamicFee = rejectDynamicFee;
        }

        // Pre-Berlin: reject both type-1 and type-2.
        public static readonly PreLondonTxTypeRule PreBerlin = new PreLondonTxTypeRule(rejectAccessList: true, rejectDynamicFee: true);
        // Berlin/Berlin-only forks: type-1 allowed, reject type-2.
        public static readonly PreLondonTxTypeRule BerlinOnly = new PreLondonTxTypeRule(rejectAccessList: false, rejectDynamicFee: true);

        public void Validate(TransactionExecutionContext ctx, HardforkConfig config)
        {
            if (_rejectDynamicFee && ctx.IsEip1559)
                throw new TransactionValidationException("TR_TypeNotSupported");

            // Type-1 (EIP-2930 access list) tx is signalled by a non-null,
            // non-empty AccessList and the absence of EIP-1559 dynamic-fee
            // fields (type-2 carries both). At pre-Berlin forks the rule
            // ctor sets _rejectAccessList=true. Without this guard our
            // executor silently runs the type-1 tx and diverges from
            // canonical "reject + zero gas charged" state — exposed by
            // accessListExample.json at Istanbul.
            if (_rejectAccessList && !ctx.IsEip1559 && ctx.AccessList != null && ctx.AccessList.Count > 0)
                throw new TransactionValidationException("TR_TypeNotSupported");
        }
    }
}
