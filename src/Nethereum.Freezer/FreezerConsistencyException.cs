using System;

namespace Nethereum.Freezer
{
    public class FreezerConsistencyException : Exception
    {
        public FreezerConsistencyException(string message) : base(message)
        {
        }

        public FreezerConsistencyException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
