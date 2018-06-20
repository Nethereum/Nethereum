using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nethereum.ABI.Decoders
{
    public class StringBytes32Decoder:ICustomRawDecoder<string>
    {
        public string Decode(byte[] output)
        {
            if(output.Length > 32)
            {
                //assuming that first 32 is the data index as this is the raw data
                return new StringTypeDecoder().Decode(output.Skip(32).ToArray());
            }
            else
            {
                return new Bytes32TypeDecoder().Decode<string>(output);
            }
        }
    }
}
