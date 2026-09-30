using Nethereum.Model;

namespace Nethereum.EVM.Execution.TransactionValidation.Rules
{
    public sealed class SupportedTransactionTypeRule : ITransactionValidationRule
    {
        private readonly int _maxSupportedType;

        public SupportedTransactionTypeRule(int maxSupportedType)
        {
            _maxSupportedType = maxSupportedType;
        }

        public static readonly SupportedTransactionTypeRule LegacyOnly = new SupportedTransactionTypeRule(0);
        public static readonly SupportedTransactionTypeRule UpToAccessList = new SupportedTransactionTypeRule(1);
        public static readonly SupportedTransactionTypeRule UpToFeeMarket = new SupportedTransactionTypeRule(2);
        public static readonly SupportedTransactionTypeRule UpToBlob = new SupportedTransactionTypeRule(3);
        public static readonly SupportedTransactionTypeRule UpToSetCode = new SupportedTransactionTypeRule(4);

        public void Validate(TransactionExecutionContext ctx, HardforkConfig config)
        {
            if (ctx.TransactionType.AsChainByteType() > _maxSupportedType)
                throw new TransactionValidationException(TransactionError.TransactionTypeNotSupported, "TR_TypeNotSupported");
        }
    }
}
