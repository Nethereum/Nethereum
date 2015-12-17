using System;
using System.Collections.Generic;
using Ethereum.RPC.ABI;

namespace Ethereum.RPC.ABI
{
    public class StringType : BytesType
    {
        public StringType() : base("string")
        {
        }

        public override byte[] Encode(object value)
        {
            if (!(value is string))
            {
                throw new Exception("String value expected for type 'string'");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes((string) value);
            //We don't need to check Endian for UTF8
            return base.Encode(bytes, false);
        }

        public override object Decode(byte[] encoded)
        {
            return System.Text.Encoding.UTF8.GetString(encoded, 32, EncoderDecoderHelpes.GetNumberOfBytes(encoded));
        }

       
    }
}