using System;

namespace Nethereum.ChainNode.Hosting.Configuration
{
    public static class ChainNodeProducerValidator
    {
        public static void Validate(ChainNodeProducerConfig producer, string sectionPath)
        {
            if (producer.BlockGasLimit.HasValue && producer.BlockGasLimit.Value <= 0)
                throw new InvalidOperationException($"{sectionPath}:BlockGasLimit must be positive");
        }
    }
}
