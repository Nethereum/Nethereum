using Nethereum.Documentation;
using System.Numerics;
using Nethereum.CoreChain;

namespace Nethereum.ChainNode.Hosting.Configuration
{
    public static class ChainNodeProducerConfigExtensions
    {
        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-node-config", "Apply the shared Producer section to a chain config")]
        public static void ApplyTo(this ChainNodeProducerConfig producer, ChainConfig target)
        {
            if (producer.BlockGasLimit.HasValue) target.BlockGasLimit = producer.BlockGasLimit.Value;
            if (producer.BaseFee.HasValue) target.BaseFee = new BigInteger(producer.BaseFee.Value);
        }
    }
}
