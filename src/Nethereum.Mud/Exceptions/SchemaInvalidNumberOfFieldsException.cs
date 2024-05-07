using Nethereum.Mud.EncodingDecoding;
using System;

namespace Nethereum.Mud.Exceptions
{
    public class SchemaInvalidNumberOfFieldsException : Exception
    {
        public SchemaInvalidNumberOfFieldsException(string message) : base(message)
        {
        }

        public SchemaInvalidNumberOfFieldsException() : base("Invalid number of schema fields maximum number is: " + SchemaEncoder.MAX_NUMBER_OF_FIELDS)
        {
        }

    }
}
