using System;

namespace Nethereum.ABI.Encoders
{
    public class BoolTypeEncoder : ITypeEncoder
    {
        private IntTypeEncoder intTypeEncoder;

        public BoolTypeEncoder()
        {
            this.intTypeEncoder = new IntTypeEncoder();
        }

        public byte[] Encode(object value)
        {
            if (!(value is bool))
            {
                throw new Exception("Wrong value for bool type: " + value);
            }
            return intTypeEncoder.Encode((bool) value ? 1 : 0);
        }
    }
}