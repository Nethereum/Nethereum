using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.Util
{
    public class EthereumMessageHasher
    {
        public byte[] HashPrefixedMessage(string plainMessage)
        {
            return HashPrefixedMessage(Encoding.UTF8.GetBytes(plainMessage));
        }

        public byte[] HashPrefixedMessage(byte[] message)
        {
            var byteList = new List<byte>();
            var bytePrefix = "0x19".HexToByteArray();
            var textBytePrefix = Encoding.UTF8.GetBytes("Ethereum Signed Message:\n" + message.Length);

            byteList.AddRange(bytePrefix);
            byteList.AddRange(textBytePrefix);
            byteList.AddRange(message);
            return new DefaultMessageHasher().Hash(byteList.ToArray());
        }
    }
}
