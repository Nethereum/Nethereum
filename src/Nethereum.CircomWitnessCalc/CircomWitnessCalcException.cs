using System;

namespace Nethereum.CircomWitnessCalc
{
    public class CircomWitnessCalcException : Exception
    {
        public CircomWitnessCalcException(string message) : base(message) { }
        public CircomWitnessCalcException(string message, Exception innerException) : base(message, innerException) { }
    }
}
