using System;

namespace Nethereum.PrivacyPools
{
    public enum PrivacyPoolErrorCode
    {
        InvalidValue,
        MerkleError,
        ProofGenerationFailed,
        ProofVerificationFailed,
        ContractCallFailed,
        PoolNotFound,
        AccountRecoveryFailed,
        CommitmentNotFound,
        NullifierAlreadySpent,
        InsufficientBalance,
        CircuitNotAvailable,
        ASPRootMismatch
    }

    public class PrivacyPoolException : Exception
    {
        public PrivacyPoolErrorCode ErrorCode { get; }

        public PrivacyPoolException(PrivacyPoolErrorCode code, string message)
            : base(message)
        {
            ErrorCode = code;
        }

        public PrivacyPoolException(PrivacyPoolErrorCode code, string message, Exception inner)
            : base(message, inner)
        {
            ErrorCode = code;
        }
    }

    public class ProofException : PrivacyPoolException
    {
        public ProofException(string message)
            : base(PrivacyPoolErrorCode.ProofGenerationFailed, message) { }

        public ProofException(string message, Exception inner)
            : base(PrivacyPoolErrorCode.ProofGenerationFailed, message, inner) { }

        public static ProofException GenerationFailed(string circuit, Exception inner) =>
            new ProofException($"Failed to generate {circuit} proof", inner);

        public static ProofException VerificationFailed(string reason) =>
            new ProofException(reason)
            { };
    }

    public class ContractException : PrivacyPoolException
    {
        public string TransactionHash { get; }

        public ContractException(string message, string txHash = null)
            : base(PrivacyPoolErrorCode.ContractCallFailed, message)
        {
            TransactionHash = txHash;
        }

        public ContractException(string message, Exception inner)
            : base(PrivacyPoolErrorCode.ContractCallFailed, message, inner) { }

        public static ContractException DepositFailed(string txHash = null) =>
            new ContractException("Deposit transaction failed", txHash);

        public static ContractException WithdrawalFailed(string txHash = null) =>
            new ContractException("Withdrawal transaction failed", txHash);

        public static ContractException RagequitFailed(string txHash = null) =>
            new ContractException("Ragequit transaction failed", txHash);

        public static ContractException PoolNotFound(string asset) =>
            new ContractException($"No pool registered for asset {asset}")
            { };
    }

    public class AccountRecoveryException : PrivacyPoolException
    {
        public AccountRecoveryException(string message)
            : base(PrivacyPoolErrorCode.AccountRecoveryFailed, message) { }

        public AccountRecoveryException(string message, Exception inner)
            : base(PrivacyPoolErrorCode.AccountRecoveryFailed, message, inner) { }
    }

    public class CircuitException : PrivacyPoolException
    {
        public string CircuitName { get; }

        public CircuitException(string circuitName, string message)
            : base(PrivacyPoolErrorCode.CircuitNotAvailable, message)
        {
            CircuitName = circuitName;
        }

        public static CircuitException NotAvailable(string circuitName) =>
            new CircuitException(circuitName, $"Circuit '{circuitName}' artifacts not available");
    }
}
