namespace Nethereum.DevP2P.Sync.Mempool
{
    public sealed class MempoolSubmitResult
    {
        public bool Accepted { get; }

        public byte[] TransactionHash { get; }

        public string Sender { get; }

        public MempoolRejectReason RejectReason { get; }
        public string RejectMessage { get; }

        private MempoolSubmitResult(bool accepted, byte[] hash, string sender,
            MempoolRejectReason rejectReason, string rejectMessage)
        {
            Accepted = accepted;
            TransactionHash = hash;
            Sender = sender;
            RejectReason = rejectReason;
            RejectMessage = rejectMessage;
        }

        public static MempoolSubmitResult FromAccepted(byte[] hash, string sender)
            => new MempoolSubmitResult(true, hash, sender, MempoolRejectReason.None, null);

        public static MempoolSubmitResult FromRejected(MempoolAdmission admission)
            => new MempoolSubmitResult(false, null, admission.Sender,
                admission.Reason, admission.RejectReason);
    }
}
