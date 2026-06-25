namespace Nethereum.CoreChain.Proving
{
    public enum WitnessRetentionMode { UntilProven, Days, Blocks, Forever }

    public class WitnessRetentionPolicy
    {
        public WitnessRetentionMode Mode { get; set; }
        public int Value { get; set; }

        public static WitnessRetentionPolicy UntilProven => new WitnessRetentionPolicy { Mode = WitnessRetentionMode.UntilProven };
        public static WitnessRetentionPolicy Forever => new WitnessRetentionPolicy { Mode = WitnessRetentionMode.Forever };
        public static WitnessRetentionPolicy Days(int n) => new WitnessRetentionPolicy { Mode = WitnessRetentionMode.Days, Value = n };
        public static WitnessRetentionPolicy Blocks(int n) => new WitnessRetentionPolicy { Mode = WitnessRetentionMode.Blocks, Value = n };
    }
}
