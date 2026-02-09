namespace Nethereum.AccountAbstraction.Services
{
    public class RecoveryRequest
    {
        public byte[] RecoveryId { get; set; } = null!;
        public string Account { get; set; } = null!;
        public string NewOwner { get; set; } = null!;
        public string InitiatedBy { get; set; } = null!;
        public DateTimeOffset InitiatedAt { get; set; }
        public DateTimeOffset ExecutableAt { get; set; }
        public int ApprovalsCount { get; set; }
        public int RequiredApprovals { get; set; }
        public string[] Approvers { get; set; } = Array.Empty<string>();
        public RecoveryStatus Status { get; set; }
    }

    public enum RecoveryStatus
    {
        Pending,
        Ready,
        Executed,
        Cancelled
    }
}
