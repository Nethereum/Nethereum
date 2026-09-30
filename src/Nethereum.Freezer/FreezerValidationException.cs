using System;

namespace Nethereum.Freezer
{
    public class FreezerValidationException : Exception
    {
        public FreezerValidationException(string message) : base(message)
        {
        }

        public FreezerValidationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
