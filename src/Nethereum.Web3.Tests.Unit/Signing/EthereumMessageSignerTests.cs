using Nethereum.ABI.Util;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using System.Text;
using Xunit;

namespace Nethereum.Web3.Tests.Unit
{
    public class EthereumMessageSignerTests
    {
        [Fact]
        public void ShouldVerifyMEWSignatureEncodingMessageAsUTF8()
        {
            var address = "0xe651c5051ce42241765bbb24655a791ff0ec8d13";
            var msg = "wee test message 18/09/2017 02:55PM";
            var sig = "0xf5ac62a395216a84bd595069f1bb79f1ee08a15f07bb9d9349b3b185e69b20c60061dbe5cdbe7b4ed8d8fea707972f03c21dda80d99efde3d96b42c91b2703211b";

            var signer = new EthereumMessageSigner();
            var addressRec = signer.EncodeUTF8AndEcRecover(msg, sig);
            Assert.Equal(address.ToLower(), addressRec.ToLower());
        }

        [Fact]
        public void ShouldSignAndVerifyEncodingMessageAsUTF8()
        {
            var address = "0x12890d2cce102216644c59dae5baed380d84830c";
            var msg = "wee test message 18/09/2017 02:55PM";
            var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
            var signer = new EthereumMessageSigner();
            var signature = signer.EncodeUTF8AndSign(msg, new EthECKey(privateKey));
            var addressRec = signer.EncodeUTF8AndEcRecover(msg, signature);
            Assert.Equal(address.ToLower(), addressRec.ToLower());
        }

        [Fact]
        public void ShouldRecover()
        {
            var signature = "0x0976a177078198a261faf206287b8bb93ebb233347ab09a57c8691733f5772f67f398084b30fc6379ffee2cc72d510fd0f8a7ac2ee0162b95dc5d61146b40ffa1c";
            var text = "test";
            var hasher = new Sha3Keccack();
            var hash = hasher.CalculateHash(text);
            var signer = new EthereumMessageSigner();
            var account = signer.EcRecover(hash.HexToByteArray(), signature);
            Assert.Equal("0x12890d2cce102216644c59dae5baed380d84830c", account.EnsureHexPrefix().ToLower());

            signature = signer.Sign(hash.HexToByteArray(),
                "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");

            account = signer.EcRecover(hash.HexToByteArray(), signature);

            Assert.Equal("0x12890d2cce102216644c59dae5baed380d84830c".ToLower(), account.EnsureHexPrefix().ToLower());
        }

        [Fact]
        public void ShouldRecoverUsingShortcutHashes()
        {
            var signature = "0x0976a177078198a261faf206287b8bb93ebb233347ab09a57c8691733f5772f67f398084b30fc6379ffee2cc72d510fd0f8a7ac2ee0162b95dc5d61146b40ffa1c";
            var text = "test";
            var signer = new EthereumMessageSigner();
            var account = signer.HashAndEcRecover(text, signature);
            Assert.Equal("0x12890d2cce102216644c59dae5baed380d84830c", account.EnsureHexPrefix().ToLower());

            signature = signer.HashAndSign(text,
                "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");

            account = signer.HashAndEcRecover(text, signature);

            Assert.Equal("0x12890d2cce102216644c59dae5baed380d84830c".ToLower(), account.EnsureHexPrefix().ToLower());
        }
        
    }
}