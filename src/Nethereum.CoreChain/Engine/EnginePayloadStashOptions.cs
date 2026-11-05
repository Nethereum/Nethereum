using Nethereum.Documentation;
namespace Nethereum.CoreChain.Engine
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "EnginePayloadStashOptions - the bounds on retained newPayload blocks")]
    public sealed class EnginePayloadStashOptions
    {
        public const int DefaultMaxBlocks = 128;

        public const long DefaultMaxBytes = 256L * 1024 * 1024;

        public int MaxBlocks { get; set; } = DefaultMaxBlocks;

        public long MaxBytes { get; set; } = DefaultMaxBytes;
    }
}
