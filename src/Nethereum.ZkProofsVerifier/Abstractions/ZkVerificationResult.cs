namespace Nethereum.ZkProofsVerifier.Abstractions
{
    public class ZkVerificationResult
    {
        public bool IsValid { get; }
        public string Error { get; }

        private ZkVerificationResult(bool isValid, string error)
        {
            IsValid = isValid;
            Error = error;
        }

        public static ZkVerificationResult Valid()
        {
            return new ZkVerificationResult(true, null);
        }

        public static ZkVerificationResult Invalid(string reason)
        {
            return new ZkVerificationResult(false, reason);
        }
    }
}
