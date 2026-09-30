namespace Nethereum.X402.Models;

public static class X402ErrorCodes
{
    // exact / EIP-3009 verify (v2 vocabulary)
    public const string InvalidScheme = "invalid_exact_evm_scheme";
    public const string UnsupportedPayloadType = "unsupported_payload_type";
    public const string NetworkMismatch = "invalid_exact_evm_network_mismatch";
    public const string MissingEip712Domain = "invalid_exact_evm_missing_eip712_domain";
    public const string RecipientMismatch = "invalid_exact_evm_recipient_mismatch";
    public const string InvalidSignature = "invalid_exact_evm_signature";
    public const string InvalidValidBefore = "invalid_exact_evm_payload_authorization_valid_before";
    public const string InvalidValidAfter = "invalid_exact_evm_payload_authorization_valid_after";
    public const string InvalidValue = "invalid_exact_evm_authorization_value";
    public const string UndeployedSmartWallet = "invalid_exact_evm_payload_undeployed_smart_wallet";
    public const string TransactionFailed = "invalid_exact_evm_transaction_failed";
    public const string TokenNameMismatch = "invalid_exact_evm_token_name_mismatch";
    public const string TokenVersionMismatch = "invalid_exact_evm_token_version_mismatch";
    public const string Eip3009NotSupported = "invalid_exact_evm_eip3009_not_supported";
    public const string NonceAlreadyUsed = "invalid_exact_evm_nonce_already_used";
    public const string InsufficientFunds = "invalid_exact_evm_insufficient_balance";
    public const string TransactionSimulationFailed = "invalid_exact_evm_transaction_simulation_failed";

    public const string Permit2InvalidSpender = "invalid_permit2_spender";
    public const string Permit2RecipientMismatch = "invalid_permit2_recipient_mismatch";
    public const string Permit2DeadlineExpired = "permit2_deadline_expired";
    public const string Permit2NotYetValid = "permit2_not_yet_valid";
    public const string Permit2AmountMismatch = "permit2_amount_mismatch";
    public const string Permit2TokenMismatch = "permit2_token_mismatch";
    public const string Permit2InvalidSignature = "invalid_permit2_signature";
    public const string Permit2AllowanceRequired = "permit2_allowance_required";
    public const string Permit2SimulationFailed = "permit2_simulation_failed";
    public const string Permit2InsufficientBalance = "permit2_insufficient_balance";
    public const string Permit2ProxyNotDeployed = "permit2_proxy_not_deployed";

    public const string UnsupportedScheme = "unsupported_payload_type";
    public const string InvalidTransactionState = "invalid_transaction_state";

    public const string InvalidPayload = "invalid_payload";
    public const string InvalidNetwork = "invalid_network";
    public const string InvalidPaymentRequirements = "invalid_payment_requirements";
    public const string InvalidX402Version = "invalid_x402_version";
    public const string UnexpectedVerifyError = "unexpected_verify_error";
    public const string UnexpectedSettleError = "unexpected_settle_error";

    public const string PaymentRequired = "payment_required";

    public const string FacilitatorError = "facilitator_error";

    /// <summary>An EIP-3009 cancelAuthorization transaction failed to submit or confirm.</summary>
    public const string CancellationError = "cancellation_error";
}
