using System;

namespace Nethereum.EVM.BlockchainState
{
    public class EvmHostException : Exception
    {
        public EvmHostException(string message) : base(message) { }
        public EvmHostException(string message, Exception inner) : base(message, inner) { }

        public static bool IsHostOrSystemFault(Exception ex) =>
            ex is EvmHostException || ex is OperationCanceledException || ex is OutOfMemoryException;
    }
}
