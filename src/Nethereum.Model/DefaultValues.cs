using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.Model
{
    public class DefaultValues
    {

        public static DefaultValues Current { get; } = new DefaultValues();

        public static byte[] EMPTY_BYTE_ARRAY = new byte[0];
        public static readonly byte[] ZERO_BYTE_ARRAY = { 0 };
        public static readonly byte[] EMPTY_DATA_HASH = Sha3Keccack.Current.CalculateHash(EMPTY_BYTE_ARRAY);
        public static readonly byte[] EMPTY_TRIE_HASH = Sha3Keccack.Current.CalculateHash(RLP.RLP.EncodeElement(EMPTY_BYTE_ARRAY));

        public static readonly byte[] EMPTY_TRIE_ROOT =
            "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

        public static readonly byte[] EMPTY_UNCLES_HASH =
            "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();
    }
}
