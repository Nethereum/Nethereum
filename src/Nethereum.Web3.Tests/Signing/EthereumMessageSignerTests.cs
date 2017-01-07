using Nethereum.ABI.Util;
using Nethereum.Core;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.Web3.Tests
{
    public class EthereumMessageSignerTests
    {
        [Fact]
        public void ShouldRecover()
        {
            var signature = "0x0976a177078198a261faf206287b8bb93ebb233347ab09a57c8691733f5772f67f398084b30fc6379ffee2cc72d510fd0f8a7ac2ee0162b95dc5d61146b40ffa1c";
            var text = "test";
            var hasher = new Sha3Keccack();
            var hash = hasher.CalculateHash(text);
            var signer = new EthereumMessageSigner();
            var account = signer.EcRecover(hash.HexToByteArray(), signature);
            Assert.Equal("0x12890d2cce102216644c59dae5baed380d84830c", account.EnsureHexPrefix());

            signature = signer.Sign(hash.HexToByteArray(),
                "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");

            account = signer.EcRecover(hash.HexToByteArray(), signature);

            Assert.Equal("0x12890d2cce102216644c59dae5baed380d84830c", account.EnsureHexPrefix());
        }

        [Fact]
        public void ShouldRecoverUsingShortcutHashes()
        {
            var signature = "0x0976a177078198a261faf206287b8bb93ebb233347ab09a57c8691733f5772f67f398084b30fc6379ffee2cc72d510fd0f8a7ac2ee0162b95dc5d61146b40ffa1c";
            var text = "test";
            var signer = new EthereumMessageSigner();
            var account = signer.HashAndEcRecover(text, signature);
            Assert.Equal("0x12890d2cce102216644c59dae5baed380d84830c", account.EnsureHexPrefix());

            signature = signer.HashAndSign(text,
                "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");

            account = signer.HashAndEcRecover(text, signature);

            Assert.Equal("0x12890d2cce102216644c59dae5baed380d84830c", account.EnsureHexPrefix());
        }
    }
}