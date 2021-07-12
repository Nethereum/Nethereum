using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class EthereumMessageSignerTests
    {
        [Fact]
        public void ShouldRecover()
        {
            var signature =
                "0x0976a177078198a261faf206287b8bb93ebb233347ab09a57c8691733f5772f67f398084b30fc6379ffee2cc72d510fd0f8a7ac2ee0162b95dc5d61146b40ffa1c";
            var text = "test";
            var hasher = new Sha3Keccack();
            var hash = hasher.CalculateHash(text);
            var signer = new EthereumMessageSigner();
            var account = signer.EcRecover(hash.HexToByteArray(), signature);
            Assert.Equal("0x12890D2cce102216644c59daE5baed380d84830c", account.EnsureHexPrefix());

            signature = signer.Sign(hash.HexToByteArray(),
                "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");

            account = signer.EcRecover(hash.HexToByteArray(), signature);

            Assert.Equal("0x12890D2cce102216644c59daE5baed380d84830c", account.EnsureHexPrefix());
        }

#if NETCOREAPP3_1

        [Fact]
        public void ShouldRecoverSignedRec()
        {
            EthECKey.SignRecoverable = true;
            var signature =
                "0x0976a177078198a261faf206287b8bb93ebb233347ab09a57c8691733f5772f67f398084b30fc6379ffee2cc72d510fd0f8a7ac2ee0162b95dc5d61146b40ffa1c";
            var text = "test";
            var hasher = new Sha3Keccack();
            var hash = hasher.CalculateHash(text);
            var signer = new EthereumMessageSigner();
            var account = signer.EcRecover(hash.HexToByteArray(), signature);
            Assert.Equal("0x12890D2cce102216644c59daE5baed380d84830c", account.EnsureHexPrefix());

            signature = signer.Sign(hash.HexToByteArray(),
                "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");

            account = signer.EcRecover(hash.HexToByteArray(), signature);

            Assert.Equal("0x12890D2cce102216644c59daE5baed380d84830c", account.EnsureHexPrefix());
        }

#endif
        //[Fact]
        //public void ShouldRecoverTrezor()
        //{
        //    var signature =
        //        "0x6f7ac0bd83c951eb5730810ed0177e3d1e94fb792ba38c6765c95f53d2e2c8867891b05772b6b1c573c0aca1765d74f7fc9955c1ac449654eb75c57de7d48c121b";
        //    var msg = "this is message sign with Trezor, 11-Mar-2018 08:58";
        //    var signer = new EthereumMessageSigner();
        //    var hasher = new Sha3Keccack();
        //    var hash = hasher.CalculateHash(msg);
        //    var addressRec = signer.EcRecover(hash.HexToByteArray(), signature);
        //    Assert.Equal("0x0dcfcc9c06f5cf4e3f0e22eaae49b2d93bd5adf5".ToLower(), addressRec.ToLower());
        //}

        [Fact]
        public void ShouldRecoverUsingShortcutHashes()
        {
            var signature =
                "0x0976a177078198a261faf206287b8bb93ebb233347ab09a57c8691733f5772f67f398084b30fc6379ffee2cc72d510fd0f8a7ac2ee0162b95dc5d61146b40ffa1c";
            var text = "test";
            var signer = new EthereumMessageSigner();
            var account = signer.HashAndEcRecover(text, signature);
            Assert.True("0x12890D2cce102216644c59daE5baed380d84830c".IsTheSameAddress(account.EnsureHexPrefix()));

            signature = signer.HashAndSign(text,
                "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");

            account = signer.HashAndEcRecover(text, signature);
            Assert.True("0x12890D2cce102216644c59daE5baed380d84830c".IsTheSameAddress(account.EnsureHexPrefix()));
        }

        [Fact]
        public void ShouldSignAndVerifyEncodingMessageAsUTF8()
        {
            var address = "0x12890D2cce102216644c59daE5baed380d84830c";
            var msg = "wee test message 18/09/2017 02:55PM";
            var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
            var signer = new EthereumMessageSigner();
            var signature = signer.EncodeUTF8AndSign(msg, new EthECKey(privateKey));
            var addressRec = signer.EncodeUTF8AndEcRecover(msg, signature);
            Assert.Equal(address, addressRec);
        }

        [Fact]
        public void ShouldVerifyMEWSignatureEncodingMessageAsUTF8()
        {
            var address = "0xe651c5051ce42241765bbb24655a791ff0ec8d13";
            var msg = "wee test message 18/09/2017 02:55PM";
            var sig =
                "0xf5ac62a395216a84bd595069f1bb79f1ee08a15f07bb9d9349b3b185e69b20c60061dbe5cdbe7b4ed8d8fea707972f03c21dda80d99efde3d96b42c91b2703211b";

            var signer = new EthereumMessageSigner();
            var addressRec = signer.EncodeUTF8AndEcRecover(msg, sig);
            Assert.True(address.IsTheSameAddress(addressRec));
        }

        [Fact]
        public void ShouldVerifySignatureEncodingMessageAsUTF8()
        {
            var address = "0x12890D2cce102216644c59daE5baed380d84830c";
            var msg = "Hello from Nethereum";
            var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";

            var signer = new EthereumMessageSigner();

            var signature = signer.EncodeUTF8AndSign(msg, new EthECKey(privateKey));

            var signatureExpected =
                "0xe20e42c13fbf52a5d65229f4dd1dcd3255691166ce2852456631baf4836afd4630480609a76794ee3018c5514ee3a0592031cf2490e7356dffe4ed202606f5181c";

            Assert.Equal(signatureExpected, signature);

            var addressRec = signer.EncodeUTF8AndEcRecover(msg, signatureExpected);
            Assert.Equal(address, addressRec);
        }
    }
}