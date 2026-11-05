using Nethereum.Documentation;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.TransactionValidation.Rules
{
    [NethereumDocExample(DocSection.EvmSimulator, "transaction-signature", "Per-fork transaction signature rule")]
    public sealed class TransactionSignatureValidationRule : ITransactionValidationRule
    {
        private static readonly EvmUInt256 Secp256k1N =
            EvmUInt256.Parse("0xfffffffffffffffffffffffffffffffebaaedce6af48a03bbfd25e8cd0364141");

        private static readonly EvmUInt256 HalfSecp256k1N =
            EvmUInt256.Parse("0x7fffffffffffffffffffffffffffffff5d576e7357a4501ddfe92f46681b20a0");

        public static readonly TransactionSignatureValidationRule Frontier = new TransactionSignatureValidationRule(rejectsHighS: false, acceptsEip155V: false);
        public static readonly TransactionSignatureValidationRule Homestead = new TransactionSignatureValidationRule(rejectsHighS: true, acceptsEip155V: false);
        public static readonly TransactionSignatureValidationRule SpuriousDragon = new TransactionSignatureValidationRule(rejectsHighS: true, acceptsEip155V: true);

        private readonly bool _rejectsHighS;
        private readonly bool _acceptsEip155V;

        private TransactionSignatureValidationRule(bool rejectsHighS, bool acceptsEip155V)
        {
            _rejectsHighS = rejectsHighS;
            _acceptsEip155V = acceptsEip155V;
        }

        public void Validate(TransactionExecutionContext ctx, HardforkConfig config)
        {
            var signature = ctx.Signature;
            if (signature == null) return;

            var r = EvmUInt256.FromBigEndian(signature.R);
            var s = EvmUInt256.FromBigEndian(signature.S);
            var v = EvmUInt256.FromBigEndian(signature.V);

            if (r.IsZero || r >= Secp256k1N) Reject("bad r");
            if (s.IsZero || s >= Secp256k1N) Reject("bad s");
            if (_rejectsHighS && s > HalfSecp256k1N) Reject("bad s");

            if (ctx.TransactionType == TransactionType.LegacyTransaction || ctx.TransactionType == TransactionType.LegacyChainTransaction)
            {
                var isUnprotectedV = v == new EvmUInt256(27) || v == new EvmUInt256(28);
                if (!isUnprotectedV && !(_acceptsEip155V && v >= new EvmUInt256(35))) Reject("bad v");
            }
            else if (v > EvmUInt256.One)
            {
                Reject("bad y_parity");
            }
        }

        private static void Reject(string reason) =>
            throw new TransactionValidationException(TransactionError.InvalidSignature, "INVALID_SIGNATURE_VRS: " + reason);
    }
}
