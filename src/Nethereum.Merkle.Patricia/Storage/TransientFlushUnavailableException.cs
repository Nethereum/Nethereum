using System;

namespace Nethereum.Merkle.Patricia.Storage
{
    public sealed class TransientFlushUnavailableException : Exception
    {
        public TransientFlushUnavailableException(string message)
            : base(message) { }

        public TransientFlushUnavailableException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
