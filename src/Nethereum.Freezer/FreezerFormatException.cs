using System;

namespace Nethereum.Freezer
{
    public class FreezerFormatException : Exception
    {
        public FreezerFormatException(string message) : base(message)
        {
        }

        public FreezerFormatException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
