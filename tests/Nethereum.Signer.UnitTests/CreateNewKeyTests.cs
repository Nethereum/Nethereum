using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class CreateNewKeyTests
    {
        [Fact]
        public void ShouldCreateAKeyOf32Bytes()
        {
            for (int i = 0; i < 10000; i++)
            {
                var key = EthECKey.GenerateKey();
                Assert.True(key.GetPrivateKeyAsBytes().Length == 32);
            }
        }

        [Fact]
        public void ShouldCreateDifferentKeysUsingRandomSeeds()
        {
            //this is using random so they will do anyway, but somebody thought that the seed was to create the same key, so keeping it here for reference
            var key = EthECKey.GenerateKey(Sha3Keccack.Current.CalculateHash("monkey").HexToByteArray());
            var key2 = EthECKey.GenerateKey(Sha3Keccack.Current.CalculateHash("monkey2").HexToByteArray());

            Assert.True(key.GetPrivateKey() != key2.GetPrivateKey());
        }
    }
}