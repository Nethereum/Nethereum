namespace Nethereum.DevP2P.Sync.Mempool
{
    public enum MempoolRejectReason
    {
        None = 0,

        Oversize,

        InvalidSignature,

        WrongChainId,

        IntrinsicGasTooLow,

        GasLimitTooHigh,

        FeeCapBelowPriorityFee,

        NonceTooLow,

        InsufficientBalance,

        BlobSidecarMissing,

        BlobCountInvalid,

        BlobVersionedHashInvalid,
    }

    public sealed class MempoolAdmission
    {
        public bool Accepted { get; }
        public MempoolRejectReason Reason { get; }
        public string RejectReason { get; }

        public string Sender { get; }

        private MempoolAdmission(bool accepted, MempoolRejectReason reason, string message, string sender)
        {
            Accepted = accepted;
            Reason = reason;
            RejectReason = message;
            Sender = sender;
        }

        public static MempoolAdmission Accept(string sender)
            => new MempoolAdmission(true, MempoolRejectReason.None, null, sender);

        public static MempoolAdmission Reject(MempoolRejectReason reason, string message, string sender = null)
            => new MempoolAdmission(false, reason, message, sender);
    }
}
