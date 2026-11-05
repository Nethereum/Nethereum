namespace Nethereum.X402.Models;

public static class X402ErrorCodes
{
    public const string InvalidScheme = "invalid_exact_evm_scheme";
    public const string UnsupportedPayloadType = "unsupported_payload_type";
    public const string NetworkMismatch = "invalid_exact_evm_network_mismatch";
    public const string MissingEip712Domain = "invalid_exact_evm_missing_eip712_domain";
    public const string RecipientMismatch = "invalid_exact_evm_recipient_mismatch";
    public const string InvalidSignature = "invalid_exact_evm_signature";
    public const string InvalidValidBefore = "invalid_exact_evm_payload_authorization_valid_before";
    public const string InvalidValidAfter = "invalid_exact_evm_payload_authorization_valid_after";
    public const string InvalidValue = "invalid_exact_evm_authorization_value";
    public const string AuthorizationValueMismatch = "invalid_exact_evm_payload_authorization_value_mismatch";
    public const string TransferEventMismatch = "invalid_exact_evm_transfer_event_mismatch";
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
    public const string Permit2612AmountMismatch = "permit2_2612_amount_mismatch";
    public const string Permit2InvalidNonce = "permit2_invalid_nonce";
    public const string Permit2InvalidAmount = "permit2_invalid_amount";
    public const string Permit2InvalidDestination = "permit2_invalid_destination";
    public const string Permit2InvalidOwner = "permit2_invalid_owner";
    public const string Permit2PaymentTooEarly = "permit2_payment_too_early";

    public const string InvalidEip2612ExtensionFormat = "invalid_eip2612_extension_format";
    public const string Eip2612FromMismatch = "eip2612_from_mismatch";
    public const string Eip2612AssetMismatch = "eip2612_asset_mismatch";
    public const string Eip2612SpenderNotPermit2 = "eip2612_spender_not_permit2";
    public const string Eip2612DeadlineExpired = "eip2612_deadline_expired";

    public const string InvalidErc20ApprovalExtensionFormat = "invalid_erc20_approval_extension_format";
    public const string Erc20ApprovalFromMismatch = "erc20_approval_from_mismatch";
    public const string Erc20ApprovalAssetMismatch = "erc20_approval_asset_mismatch";
    public const string Erc20ApprovalSpenderNotPermit2 = "erc20_approval_spender_not_permit2";
    public const string Erc20ApprovalTxWrongTarget = "erc20_approval_tx_wrong_target";
    public const string Erc20ApprovalTxWrongSelector = "erc20_approval_tx_wrong_selector";
    public const string Erc20ApprovalTxWrongSpender = "erc20_approval_tx_wrong_spender";
    public const string Erc20ApprovalTxInvalidCalldata = "erc20_approval_tx_invalid_calldata";
    public const string Erc20ApprovalTxSignerMismatch = "erc20_approval_tx_signer_mismatch";
    public const string Erc20ApprovalTxInvalidSignature = "erc20_approval_tx_invalid_signature";
    public const string Erc20ApprovalTxParseFailed = "erc20_approval_tx_parse_failed";
    public const string Erc20ApprovalTxFailed = "erc20_approval_tx_failed";
    public const string Erc20ApprovalBroadcastFailed = "erc20_approval_broadcast_failed";
    public const string GasFundingFailed = "gas_funding_failed";

    public const string Erc20ApprovalTxWrongAmount = "erc20_approval_tx_wrong_amount";
    public const string Erc20ApprovalTxChainIdMismatch = "erc20_approval_tx_chain_id_mismatch";
    public const string Erc20ApprovalTxValueNotZero = "erc20_approval_tx_value_not_zero";
    public const string Erc20ApprovalTxGasCostExceedsCap = "erc20_approval_tx_gas_cost_exceeds_cap";
    public const string Erc20ApprovalTxNonceMismatch = "erc20_approval_tx_nonce_mismatch";
    public const string Erc20ApprovalTxMaxFeeBelowBaseFee = "erc20_approval_tx_max_fee_below_base_fee";
    public const string Erc20ApprovalAmountBelowRequired = "erc20_approval_amount_below_required";
    public const string Erc20ApprovalTxInFlight = "erc20_approval_tx_in_flight";
    public const string Erc20ApprovalTxGasLimitTooLow = "erc20_approval_tx_gas_limit_too_low";
    public const string Erc20ApprovalTxSimulationFailed = "erc20_approval_tx_simulation_failed";
    public const string Erc20ApprovalTxUnsupportedType = "erc20_approval_tx_unsupported_type";
    public const string Erc20ApprovalTxGasLimitTooHigh = "erc20_approval_tx_gas_limit_too_high";
    public const string Erc20ApprovalTxPriorityFeeTooHigh = "erc20_approval_tx_priority_fee_too_high";
    public const string Erc20ApprovalTxMaxFeeTooHigh = "erc20_approval_tx_max_fee_too_high";
    public const string Erc20ApprovalTxPriorityFeeTooLow = "erc20_approval_tx_priority_fee_too_low";

    public const string UnsupportedScheme = "unsupported_scheme";
    public const string InvalidTransactionState = "invalid_transaction_state";
    public const string SettlementPending = "settlement_pending";
    public const string SettlementGasExceedsLimit = "settlement_gas_exceeds_limit";
    public const string AssetNotAllowed = "invalid_exact_evm_asset_not_allowed";
    public const string ReplayStoreFull = "replay_store_full";

    public const string InvalidPayload = "invalid_payload";
    public const string InvalidNetwork = "invalid_network";
    public const string InvalidPaymentRequirements = "invalid_payment_requirements";
    public const string InvalidX402Version = "invalid_x402_version";
    public const string UnexpectedVerifyError = "unexpected_verify_error";
    public const string UnexpectedSettleError = "unexpected_settle_error";

    public const string PaymentRequired = "payment_required";

    public const string FacilitatorError = "facilitator_error";

    public const string CancellationError = "cancellation_error";
}
