using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Wallet.Bip32;
using System.Diagnostics;

namespace Nethereum.Wallet.UnitTests
{
    public class MinimalHDWalletTests
    {
        public const string Words =
          "ripple scissors kick mammal hire column oak again sun offer wealth tomorrow wagon turn fatal";

        public const string Passphrase = "TREZOR";

        [Theory]
        [InlineData("0x27Ef5cDBe01777D62438AfFeb695e33fC2335979", 0)]
        [InlineData("0x98f5438cDE3F0Ff6E11aE47236e93481899d1C47", 1)]
        [InlineData("0xA4267Fb4d2300e82E16441A740996d75402a2140", 2)]
        [InlineData("0xD6D7a427d6fd40B4109ACD5a5AF455E7c02a3310", 3)]
        [InlineData("0xd94C2F0Ae3E5cc074668a4D220801C0Ab96082E1", 4)]
        [InlineData("0x9Fab8f2E3b4a9E514Ae27bDabA628E8E2840823B", 5)]
        [InlineData("0xC876B10D76857eCaB086d75d59d6FEE28C48285c", 6)]
        [InlineData("0xe2027d3Bc6a2507490eFc2b8397b464bFA47AA57", 7)]
        [InlineData("0x584A9ac0491371F066c9cbbc795d0D1aD5d08267", 8)]
        [InlineData("0x9e019Ee5D24132bddBBA857407667c0e3Ce82a90", 9)]
        [InlineData("0x6bE280E6aD7F8fBd40b5Bf12e28dCbA87E328499", 10)]
        [InlineData("0x2556f135C21A4FB2aeF401bA804666268e2fb4EC", 11)]
        [InlineData("0x7c62c432D56d5eA704C9cEC73Af3Bb183EF8B952", 12)]
        [InlineData("0x4c592d1A0BcA11eC447c56Fc7ee2858Bc3aDDe51", 13)]
        [InlineData("0x2C3B76bF48275378041cA7dF9B8E6aA8c0664847", 14)]
        [InlineData("0xeb8C5D6575C704A4bEcDd41a3592dF89389568E2", 15)]
        [InlineData("0x94050231B601A6906C80BfEd468D8C32fa7792A7", 16)]
        [InlineData("0x76933C9e4C1B635EaBB6F5273d4bd2DdFcc4379a", 17)]
        [InlineData("0x7cFfb4E2fdF3F40c210b21A972E73Df1cC3806B3", 18)]
        [InlineData("0xF07fB895F441C46264d1FABD6eb3C757A2E7f9e0", 19)]
        public void ShouldFindAccountUsingIndex(string address, int index)
        {
            var wallet = new MinimalHDWallet(Words, Passphrase);
            var seed = wallet.Seed.ToHex();
            Debug.WriteLine($"Seed: {seed}");
            var accountAddress = wallet.GetEthereumAddress(index);
            Assert.Equal(address, accountAddress);
        }

        [Fact]
        public void Test()
        {
            var wallet = new MinimalHDWallet("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about", null);
            var seed = wallet.Seed.ToHex();
            Debug.WriteLine($"Seed: {seed}");
            var accountAddress = wallet.GetEthereumAddress(0);
            Assert.Equal("0x9858EfFD232B4033E47d90003D41EC34EcaEda94", accountAddress);
        }

       
    }
}
