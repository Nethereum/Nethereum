using System;

namespace Nethereum.Freezer
{
    public class FreezerImmutableException : Exception
    {
        public FreezerImmutableException(string message) : base(message)
        {
        }

        public FreezerImmutableException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
