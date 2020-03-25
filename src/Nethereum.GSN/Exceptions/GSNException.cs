using System;

namespace Nethereum.GSN.Exceptions
{
    public class GSNException : Exception
    {
        public GSNException()
        {
        }

        public GSNException(string message)
            : base(message)
        {
        }

        public GSNException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
