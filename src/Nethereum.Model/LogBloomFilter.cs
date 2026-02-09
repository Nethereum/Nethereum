using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Model
{
    /*   
        # Ethereum Bloom Filters 

        Bloom filters are a type of data structure that use cryptographic hashes

        to help data stored within them to be retrieved or stored.  They work like other

        data structures, but in a probabilistic way: it allows for false positive

        matches but not false negative matches.  Bloom filters storage space use is

        low relative to other kinds of data structures.

        ( For more information on Bloom filters, see Wikipedia: https://en.wikipedia.org/wiki/Bloom_filter )

        Ethereum bloom filters are bloom filters implemented with the SHA-256 ("keccak") cryptographic hash function.

        To see the bloom filter used in the context of the full description of Ethereum / the "Yellow Paper" see

        DR. GAVIN WOOD - ETHEREUM: A SECURE DECENTRALISED GENERALISED TRANSACTION LEDGER, EIP-150 REVISION, FOUNDER, ETHEREUM & ETHCORE, GAVIN@ETHCORE.IO

        http://gavwood.com/Paper.pdf 
        https://github.com/ethereum/eth-bloom/blob/master/what_Is_eth-bloom.txt

       Credit: Pantheon, EhtereumJ (2015) for initial reference implementation
     */

    public class LogBloomFilter
    {
        private const int LEAST_SIGNIFICANT_BYTE = 0xFF;
        private const int LEAST_SIGNIFICANT_THREE_BITS = 0x7;

        private byte[] _data = new byte[256];
        public byte[] Data => _data;

        public LogBloomFilter()
        {
        }

        public LogBloomFilter(byte[] existingBloom)
        {
            if (existingBloom != null && existingBloom.Length == 256)
            {
                _data = existingBloom;
            }
        }

        public void AddAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return;
            var hasher = Util.Sha3Keccack.Current;
            SetBits(hasher.CalculateHash(address.HexToByteArray()));
        }

        public void AddAddress(byte[] address)
        {
            if (address == null || address.Length == 0) return;
            var hasher = Util.Sha3Keccack.Current;
            SetBits(hasher.CalculateHash(address));
        }

        public void AddTopic(byte[] topic)
        {
            if (topic == null || topic.Length == 0) return;
            var hasher = Util.Sha3Keccack.Current;
            SetBits(hasher.CalculateHash(topic));
        }

        public bool Matches(byte[] blockBloom)
        {
            if (blockBloom == null || blockBloom.Length != 256) return true;
            for (int i = 0; i < 256; i++)
            {
                if ((_data[i] & blockBloom[i]) != _data[i])
                    return false;
            }
            return true;
        }

        public bool IsEmpty()
        {
            for (int i = 0; i < 256; i++)
            {
                if (_data[i] != 0) return false;
            }
            return true;
        }

        /**
           * Discover the low order 11-bits, of the first three double-bytes, of the SHA3 hash, of each
           * value and update the bloom filter accordingly.
        */
        private void SetBits(byte[] hashValue)
        {
            for (var counter = 0; counter < 6; counter += 2)
            {
                var setBloomBit = ((hashValue[counter] & LEAST_SIGNIFICANT_THREE_BITS) << 8)
                                  + (hashValue[counter + 1] & LEAST_SIGNIFICANT_BYTE);
                SetBit(setBloomBit);
            }
        }

        private void SetBit(int index)
        {
            var byteIndex = 256 - 1 - index / 8;
            var bitIndex = index % 8;
            _data[byteIndex] = (byte)(_data[byteIndex] | (1 << bitIndex));
        }

        public void AddLog(Log log)
        {
            var hasher = Util.Sha3Keccack.Current;
            SetBits(hasher.CalculateHash(log.Address.HexToByteArray()));

            foreach (var topic in log.Topics)
            {
                SetBits(hasher.CalculateHash(topic));
            }
        }
    }
}