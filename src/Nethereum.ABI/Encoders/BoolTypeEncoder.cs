using System;

namespace Nethereum.ABI.Encoders
{
    public class BoolTypeEncoder : ITypeEncoder
    {
        private readonly IntTypeEncoder _intTypeEncoder;

        public BoolTypeEncoder()
        {
            _intTypeEncoder = new IntTypeEncoder();
        }

        public byte[] Encode(object value)
        {
            if (!(value is bool))
                throw new Exception("Wrong value for bool type: " + value);
            return _intTypeEncoder.Encode((bool) value ? 1 : 0);
        }
    }
}