using System.Collections.Generic;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.Signer
{
    public class EthereumMessageSigner : MessageSigner
    {
        public override string EcRecover(byte[] message, string signature)
        {
            return base.EcRecover(HashPrefixedMessage(message), signature);
        }

        public byte[] HashAndHashPrefixedMessage(byte[] message)
        {
            return HashPrefixedMessage(Hash(message));
        }

        public override string HashAndSign(byte[] plainMessage, EthECKey key)
        {
            return base.Sign(HashAndHashPrefixedMessage(plainMessage), key);
        }

        public byte[] HashPrefixedMessage(string plainMessage)
        {
            return HashPrefixedMessage(Encoding.UTF8.GetBytes(plainMessage));   
        }

        public byte[] HashPrefixedMessage(byte[] message)
        {
            return new EthereumMessageHasher().HashPrefixedMessage(message);
        }

        public override string Sign(byte[] message, EthECKey key)
        {
            return base.Sign(HashPrefixedMessage(message), key);
        }

        public string EncodeUTF8AndSign(string message, EthECKey key)
        {
            return base.Sign(HashPrefixedMessage(Encoding.UTF8.GetBytes(message)), key);
        }

        public string EncodeUTF8AndEcRecover(string message, string signature)
        {
            return EcRecover(Encoding.UTF8.GetBytes(message), signature);
        }
    }
}