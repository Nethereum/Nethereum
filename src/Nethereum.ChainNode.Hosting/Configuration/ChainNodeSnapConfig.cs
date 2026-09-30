namespace Nethereum.ChainNode.Hosting.Configuration
{
    public sealed class ChainNodeSnapConfig
    {
        public bool BackwardSkeletonPhase1 { get; set; } = true;

        public bool Phase1Only { get; set; }

        public bool Phase1First { get; set; }

        public bool AdvertiseSnap2 { get; set; }

        public int? SoftResponseLimit { get; set; }
    }
}
