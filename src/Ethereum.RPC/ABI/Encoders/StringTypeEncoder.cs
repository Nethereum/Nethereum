using System;

namespace Ethereum.RPC.ABI
{
    public class StringTypeEncoder : ITypeEncoder
    {
        private BytesTypeEncoder byteTypeEncoder;

        public StringTypeEncoder()
        {
            byteTypeEncoder = new BytesTypeEncoder();
        }

        public byte[] Encode(object value)
        {
            if (!(value is string))
            {
                throw new Exception("String value expected for type 'string'");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes((string)value);

            return byteTypeEncoder.Encode(bytes, false);
        }
    }
}