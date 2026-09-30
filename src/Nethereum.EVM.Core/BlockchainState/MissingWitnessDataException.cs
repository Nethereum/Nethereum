using System;

namespace Nethereum.EVM.BlockchainState
{
    public class MissingWitnessDataException : EvmHostException
    {
        public MissingWitnessDataException(string message) : base(message) { }
    }
}
