using NBitcoin;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Web3.Accounts;
using System;
using Xunit;
using Nethereum.Util;

namespace Nethereum.HdWallet.UnitTests
{
    //To validate use https://iancoleman.github.io/bip39/#english
    public class WalletTests
    {
        public const string Words =
            "ripple scissors kick mammal hire column oak again sun offer wealth tomorrow wagon turn fatal";

        public const string Password = "TREZOR";

        [Theory]
        [InlineData("0x27Ef5cDBe01777D62438AfFeb695e33fC2335979")]
        [InlineData("0x98f5438cDE3F0Ff6E11aE47236e93481899d1C47")]
        [InlineData("0xA4267Fb4d2300e82E16441A740996d75402a2140")]
        [InlineData("0xD6D7a427d6fd40B4109ACD5a5AF455E7c02a3310")]
        [InlineData("0xd94C2F0Ae3E5cc074668a4D220801C0Ab96082E1")]
        [InlineData("0x9Fab8f2E3b4a9E514Ae27bDabA628E8E2840823B")]
        [InlineData("0xC876B10D76857eCaB086d75d59d6FEE28C48285c")]
        [InlineData("0xe2027d3Bc6a2507490eFc2b8397b464bFA47AA57")]
        [InlineData("0x584A9ac0491371F066c9cbbc795d0D1aD5d08267")]
        [InlineData("0x9e019Ee5D24132bddBBA857407667c0e3Ce82a90")]
        [InlineData("0x6bE280E6aD7F8fBd40b5Bf12e28dCbA87E328499")]
        [InlineData("0x2556f135C21A4FB2aeF401bA804666268e2fb4EC")]
        [InlineData("0x7c62c432D56d5eA704C9cEC73Af3Bb183EF8B952")]
        [InlineData("0x4c592d1A0BcA11eC447c56Fc7ee2858Bc3aDDe51")]
        [InlineData("0x2C3B76bF48275378041cA7dF9B8E6aA8c0664847")]
        [InlineData("0xeb8C5D6575C704A4bEcDd41a3592dF89389568E2")]
        [InlineData("0x94050231B601A6906C80BfEd468D8C32fa7792A7")]
        [InlineData("0x76933C9e4C1B635EaBB6F5273d4bd2DdFcc4379a")]
        [InlineData("0x7cFfb4E2fdF3F40c210b21A972E73Df1cC3806B3")]
        [InlineData("0xF07fB895F441C46264d1FABD6eb3C757A2E7f9e0")]
        public void ShouldFindAccountUsingAddress(string address)
        {
            var wallet = new Wallet(Words, Password);
            var account = wallet.GetAccount(address);
            Assert.NotNull(account);
        }


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
            var wallet = new Wallet(Words, Password);
            var account = wallet.GetAccount(index);
            Assert.Equal(address, account.Address);
        }


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
        public void ShouldFindPublicKeysAccountUsingIndex(string address, int index)
        {
            var wallet = new Wallet(Words, Password);
            var publicWallet = wallet.GetMasterPublicWallet();
            var account = publicWallet.GetAddress(index);
            Assert.Equal(address, account);
        }

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
        public void ShouldFindPublicKeysAccountUsingIndexInitialisingWithPublicKey(string address, int index)
        {
            var wallet = new Wallet(Words, Password);
            var bytes = wallet.GetMasterExtPubKey().ToBytes();
            var hex = bytes.ToHex();
            Console.WriteLine(hex);
            var publicWallet = new PublicWallet(hex);

            var account = publicWallet.GetAddress(index);
            Assert.Equal(address, account);
        }


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
        public void ShouldFindPublicKeysAccountUsingIndexInitialisingWithWif(string address, int index)
        {
            var wallet = new Wallet(Words, Password);
            var wif = wallet.GetMasterExtPubKey().GetWif(Network.Main).ToWif();
            var extPubKey = ExtPubKey.Parse(wif, Network.Main);
            Console.WriteLine(wif);
            var publicWallet = new PublicWallet(extPubKey);

            var account = publicWallet.GetAddress(index);
            Assert.Equal(address, account);
        }

        [Theory]
        [InlineData("0xc4f77b4a9f5a0db3a7ffc3599e61bef986037ae9a7cc1972a10d55c030270020", 0)]
        [InlineData("0xb4ede783358d19073b53c7dab99c63265e5b5d863c133fa6ee7894c2ba53c2d8", 1)]
        [InlineData("0x8fe23ea66d3dcc6edf07c9a580f8026ea1c1ddd6774a923c2b30e9a44d8c11b5", 2)]
        [InlineData("0xd54936b42aa4e0604608fe8c2427ec1562b1e6fd0f3c1f1a56668ddb8aee99fb", 3)]
        [InlineData("0xd216a168a12c0a46d4b07b1b62cd55283d708de17dcfaf1be7c60fa31b91f180", 4)]
        [InlineData("0x27be39998ca88ca666ab33aa8e2def1da837a19d38e70f066e77132e13345e58", 5)]
        [InlineData("0x18a44a4891648f6455fe84e94cfacc1d8a9829b349499eef7653f3aff43b9b29", 6)]
        [InlineData("0xaf1a3b0ee759cc98a106eea3ed3af57ba8ceeecf1f6a050f1d6c0c36e2a4e0ee", 7)]
        [InlineData("0x993072842865e1db03605515a17a883e887db9bb12fead41c5391e535e22044c", 8)]
        [InlineData("0xd38a6a11873c939d87bf45b107ac66697ff241fa98ad5a09ff052ffdc58b42ff", 9)]
        [InlineData("0x02a1b9a345f52f216bf0dd2eabda542467825068b8d114a6cb9516ced16ad8ae", 10)]
        [InlineData("0xe48018a421e7d5100ace0d8df8064b32c4829e5490c33b5e2d49e39a72e2bc92", 11)]
        [InlineData("0x8bdb46ef9f2ff0fb021392c368b59a1188c77c12d6db561f154b869ecdd98731", 12)]
        [InlineData("0x9082bbac96a2720203b190db9c8618a8b33d6531302f29d677d95e48d96ce73c", 13)]
        [InlineData("0x4ed48e0e8320e002a928e8b84ba416823f3331177253879ec4524206bd52421b", 14)]
        [InlineData("0xe646377dc06e6b5846fa2584b98a60d7a11a24c42ed007b5ca0ba905cab9b0b2", 15)]
        [InlineData("0xe9458b547b02ad8d86c66454596269936325afb6c90c1d2fc5262bb3d0ebda6f", 16)]
        [InlineData("0x26abdeb81020564be9f79f590eaa5a730cf11aaf02b74d149858501322cc1baa", 17)]
        [InlineData("0x47b07eae780b300fa49d9a1a347df28ca35ddb3a41af2d796be32684f9b96136", 18)]
        [InlineData("0x3e9c766279c9f8e23fa03e170e41531566d1e77b338a756dc83348906d35ca8f", 19)]
        public void ShouldFindPrivateKeyUsingIndex(string privateKey, int index)
        {
            var wallet = new Wallet(Words, Password);
            var key = wallet.GetPrivateKey(index);
            Assert.Equal(privateKey, key.ToHex(true));
        }

        [Fact]
        public void ShouldCreateTheDefaultWalletUsingGivenWords()
        {
            var wallet = new Wallet(Words, Password);
            Assert.Equal(
                "7ae6f661157bda6492f6162701e570097fc726b6235011ea5ad09bf04986731ed4d92bc43cbdee047b60ea0dd1b1fa4274377c9bf5bd14ab1982c272d8076f29",
                wallet.Seed);
            var account = wallet.GetAccount(0);
            Assert.Equal("0x27Ef5cDBe01777D62438AfFeb695e33fC2335979", account.Address);
        }

        [Fact]
        public void ShouldFindAddressesUsingGivenWords()
        {
            var wallet = new Wallet(Words, Password);
            var addresses = wallet.GetAddresses(5);
            Assert.Equal("0x27Ef5cDBe01777D62438AfFeb695e33fC2335979", addresses[0]);
            Assert.Equal("0x98f5438cDE3F0Ff6E11aE47236e93481899d1C47", addresses[1]);
            Assert.Equal("0xA4267Fb4d2300e82E16441A740996d75402a2140", addresses[2]);
            Assert.Equal("0xD6D7a427d6fd40B4109ACD5a5AF455E7c02a3310", addresses[3]);
            Assert.Equal("0xd94C2F0Ae3E5cc074668a4D220801C0Ab96082E1", addresses[4]);
        }

        [Fact]
        public void ShouldMeeeeew()
        {
            var wallet = new Wallet("stem true medal chronic lion machine mask road rabbit process movie account", null, "m/44'/60'/0'/0'/x");
            var account = wallet.GetAccount(0);
            Assert.True(account.Address.IsTheSameAddress("0xd9B924d064C8D0ECFf3307e929f5a941b6A56C2D"));
        }

        [Fact]
        public void ShouldAllowDeriviationSeed()
        {
            var mnemo = new Mnemonic("stem true medal chronic lion machine mask road rabbit process movie account", Wordlist.English);
            var seed = mnemo.DeriveSeed();
            var ethWallet = new Wallet(seed);
            var account = new Account(ethWallet.GetPrivateKey(0));
            account = ethWallet.GetAccount(account.Address);
            Assert.True(account.Address.IsTheSameAddress("0x03dd02C038e15fcFbBdc372c71D595BD241E0898"));
        }
    }
}