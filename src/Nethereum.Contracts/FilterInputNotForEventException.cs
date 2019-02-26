using System;

namespace Nethereum.Contracts
{
    public class FilterInputNotForEventException : Exception
    {
        public FilterInputNotForEventException() : base("Invalid filter input for current event, the event signatures (abi types, names), or contract addresses do not match, please use the method CreateFilterInput from this class")
        {

        }
    }
}