using System;

namespace Nethereum.EVM.BlockchainState
{
    public class MissingWitnessDataException : Exception
    {
        public MissingWitnessDataException(string message) : base(message) { }
    }
}
