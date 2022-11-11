using NBitcoin;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;

namespace Nethereum.HdWallet
{
    public class PublicWallet
    {
        public ExtPubKey ExtPubKey { get; }

        public byte[] GetExtendedPublicKey()
        {
            return ExtPubKey.PubKey.ToBytes();
        }

        public PublicWallet(ExtPubKey extPubKey)
        {
            ExtPubKey = extPubKey;
        }

        public PublicWallet(ExtKey extKey)
        {
            ExtPubKey = extKey.Neuter();
        }

        public PublicWallet(byte[] extPublicKey)
            :this(new ExtPubKey(extPublicKey))
        {
     
        }

        public PublicWallet(string extPublicKey)
            :this(extPublicKey.HexToByteArray())
        {
            
        }

        private EthECKey GetEthereumKey(int index)
        {
            var key = GetExtPubKey(index);
            return new EthECKey(key.PubKey.ToBytes(), false);
        }

        public string[] GetAddresses(int numberOfAddresses = 20)
        {
            var addresses = new string[numberOfAddresses];
            for (var i = 0; i < numberOfAddresses; i++)
            {
                addresses[i] = GetAddress(i);
            }
            return addresses;
        }

        public string GetAddress(int index)
        {
            var publicKey = GetEthereumKey(index);
            if (publicKey != null)
                return publicKey.GetPublicAddress();
            return null;
        }

        public ExtPubKey GetExtPubKey(int index)
        {
            return ExtPubKey.Derive(index, false);
        }

        public PublicWallet GetChildPublicWallet(int index)
        {
            return new PublicWallet(GetExtPubKey(index));
        }

    }
}