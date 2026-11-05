namespace Nethereum.DevP2P.Sync.Snap.Healing
{
    internal sealed class PendingNode
    {
        public PendingNode(HealTask task, byte[] hash, byte[] blob, int missingChildren)
        {
            Task = task;
            Hash = hash;
            Blob = blob;
            MissingChildren = missingChildren;
        }

        public HealTask Task { get; }
        public byte[] Hash { get; }
        public byte[] Blob { get; }
        public int MissingChildren { get; set; }
    }
}
