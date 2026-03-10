using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class PersonalSignDocExampleTests
    {
        private const string PrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        private const string ExpectedAddress = "0x12890D2cce102216644c59daE5baed380d84830c";

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "personal-sign", "Sign a UTF-8 message and recover the signer address")]
        public void ShouldSignMessageAndRecoverAddress()
        {
            var signer = new EthereumMessageSigner();
            var message = "Hello from Nethereum";

            var signature = signer.EncodeUTF8AndSign(message, new EthECKey(PrivateKey));

            var recoveredAddress = signer.EncodeUTF8AndEcRecover(message, signature);
            Assert.Equal(ExpectedAddress, recoveredAddress);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "personal-sign", "Sign raw bytes and recover address")]
        public void ShouldSignBytesAndRecoverAddress()
        {
            var signer = new EthereumMessageSigner();
            var data = new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f }; // "Hello"

            var signature = signer.Sign(data, PrivateKey);
            var recoveredAddress = signer.EcRecover(data, signature);

            Assert.True(ExpectedAddress.IsTheSameAddress(recoveredAddress));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "personal-sign", "Verify a signature from any wallet (e.g. MetaMask, MEW)")]
        public void ShouldVerifyExternalWalletSignature()
        {
            var expectedAddress = "0xe651c5051ce42241765bbb24655a791ff0ec8d13";
            var message = "wee test message 18/09/2017 02:55PM";
            var signature = "0xf5ac62a395216a84bd595069f1bb79f1ee08a15f07bb9d9349b3b185e69b20c60061dbe5cdbe7b4ed8d8fea707972f03c21dda80d99efde3d96b42c91b2703211b";

            var signer = new EthereumMessageSigner();
            var recoveredAddress = signer.EncodeUTF8AndEcRecover(message, signature);

            Assert.True(expectedAddress.IsTheSameAddress(recoveredAddress));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "personal-sign", "Hash and sign shortcut methods")]
        public void ShouldUseHashAndSignShortcuts()
        {
            var signer = new EthereumMessageSigner();
            var message = "test";

            var signature = signer.HashAndSign(message, PrivateKey);
            var recovered = signer.HashAndEcRecover(message, signature);

            Assert.True(ExpectedAddress.IsTheSameAddress(recovered));
        }
    }
}
