namespace Nethereum.AppChain.Sequencer.Builder
{
    public class MudConfig
    {
        public bool Enabled { get; set; } = true;
        public string? WorldAddress { get; set; }
        public bool DeployAtGenesis { get; set; } = true;
        public byte[]? Salt { get; set; }

        public static MudConfig Default() => new()
        {
            Enabled = true,
            DeployAtGenesis = true
        };

        public static MudConfig WithPredefinedWorld(string worldAddress) => new()
        {
            Enabled = true,
            WorldAddress = worldAddress,
            DeployAtGenesis = false
        };
    }
}
