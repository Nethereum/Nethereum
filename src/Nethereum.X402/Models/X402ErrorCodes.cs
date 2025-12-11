namespace Nethereum.X402.Models;

public static class X402ErrorCodes
{
    public const string InsufficientFunds = "insufficient_funds";
    public const string InvalidSignature = "invalid_exact_evm_payload_signature";
    public const string InvalidValidAfter = "invalid_exact_evm_payload_authorization_valid_after";
    public const string InvalidValidBefore = "invalid_exact_evm_payload_authorization_valid_before";
    public const string InvalidValue = "invalid_exact_evm_payload_authorization_value";
    public const string RecipientMismatch = "invalid_exact_evm_payload_recipient_mismatch";
    public const string InvalidNetwork = "invalid_network";
    public const string InvalidPayload = "invalid_payload";
    public const string InvalidScheme = "invalid_scheme";
    public const string UnsupportedScheme = "unsupported_scheme";
    public const string NonceAlreadyUsed = "invalid_exact_evm_payload_authorization_nonce_used";
    public const string InvalidPaymentRequirements = "invalid_payment_requirements";
    public const string InvalidX402Version = "invalid_x402_version";
    public const string InvalidTransactionState = "invalid_transaction_state";
    public const string UnexpectedVerifyError = "unexpected_verify_error";
    public const string UnexpectedSettleError = "unexpected_settle_error";
}
