using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Sequencer.ProducerAuthority;

namespace Nethereum.AppChain.Server.Configuration
{
    public sealed class AppChainConsensusConfig
    {
        public AppChainConsensusMode Mode { get; set; } = AppChainConsensusMode.SingleSequencer;

        public SignerKeyPair Sequencer { get; set; } = new SignerKeyPair();

        public int BlockTimeMs { get; set; } = 1000;

        public bool AllowEmptyBlocks { get; set; }

        public BlockProductionMode BlockProductionMode { get; set; } = BlockProductionMode.Interval;

        public PolicyConfig Policy { get; set; } = PolicyConfig.OpenAccess;

        public string? ProducerAuthorityFile { get; set; }

        public string? NodeIdentity { get; set; }

        public IProducerAuthority? ProducerAuthorityOverride { get; set; }

        public AppChainCliqueConfig Clique { get; set; } = new AppChainCliqueConfig();
    }
}
