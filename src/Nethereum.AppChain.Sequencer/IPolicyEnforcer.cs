using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.AppChain.Sequencer
{
    public interface IPolicyEnforcer
    {
        PolicyConfig Policy { get; }

        Task<PolicyValidationResult> ValidateTransactionAsync(ISignedTransaction transaction);
        void UpdatePolicy(PolicyConfig policy);
        void UpdateWritersRoot(byte[] writersRoot);
    }

    public class PolicyValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public PolicyViolationType? ViolationType { get; set; }

        public static PolicyValidationResult Valid() => new PolicyValidationResult { IsValid = true };

        public static PolicyValidationResult Invalid(PolicyViolationType type, string message) => new PolicyValidationResult
        {
            IsValid = false,
            ViolationType = type,
            ErrorMessage = message
        };
    }

    public enum PolicyViolationType
    {
        UnauthorizedSender,
        CalldataTooLarge,
        BlacklistedAddress,
        InvalidSignature,
        NonceTooLow,
        InsufficientBalance
    }
}
