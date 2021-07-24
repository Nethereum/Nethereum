using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class AccesssListsRLPTests
    {
        [Fact]
        public void ShouldEncodeDecodeAccessList()
        {

            var accessLists = new List<AccessListItem>();
            accessLists.Add(new AccessListItem("0x627306090abaB3A6e1400e9345bC60c78a8BEf57",
                new List<byte[]>
                {
                    "0x3fd54831f488a22b28398de0c567a3b064b937f54f81739ae9bd545967f3abab".HexToByteArray(),
                    "0x3fd54831f488a22b28398de0c567a3b064b937f54f81739ae9bd545967f3abac".HexToByteArray()
                }
            ));
            accessLists.Add(new AccessListItem("0x627306090abaB3A6e1400e9345bC60c78a8BEf5c",
                new List<byte[]>
                {
                    "0x3fd54831f488a22b28398de0c567a3b064b937f54f81739ae9bd545967f3abaa".HexToByteArray(),
                    "0x3fd54831f488a22b28398de0c567a3b064b937f54f81739ae9bd545967f3abad".HexToByteArray()
                }
            ));

            var encodedRlp = AccessListRLPEncoderDecoder.EncodeAccessList(accessLists);
            var decodedRlp = AccessListRLPEncoderDecoder.DecodeAccessList(encodedRlp);

            Assert.True(accessLists[0].Address.IsTheSameAddress(decodedRlp[0].Address));
            Assert.Equal(accessLists[0].StorageKeys[0].ToHex(true), decodedRlp[0].StorageKeys[0].ToHex(true));
            Assert.Equal(accessLists[0].StorageKeys[1].ToHex(true), decodedRlp[0].StorageKeys[1].ToHex(true));
            Assert.True(accessLists[1].Address.IsTheSameAddress(decodedRlp[1].Address));
            Assert.Equal(accessLists[1].StorageKeys[0].ToHex(true), decodedRlp[1].StorageKeys[0].ToHex(true));
            Assert.Equal(accessLists[1].StorageKeys[1].ToHex(true), decodedRlp[1].StorageKeys[1].ToHex(true));
        }
    }
}