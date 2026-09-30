using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.IntegrationTests.Harness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.Sync.IntegrationTests
{
    public class TrustedNodeIdContractTests
    {
        private readonly ITestOutputHelper _output;

        public TrustedNodeIdContractTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void Given_APeersNodeIdInTheTrustedList_When_ThatPeerConnects_Then_ItIsRecognisedAsTrusted()
        {
            var peerKey = EthECKey.GenerateKey();
            var configured = DevChainNode.NodeIdOf(peerKey);
            var onTheWire = peerKey.GetPubKeyNoPrefix();

            _output.WriteLine($"configured  : {configured}");
            _output.WriteLine($"on the wire : {onTheWire.ToHex()}");
            _output.WriteLine($"wire bytes  : {onTheWire.Length}");

            var listener = new RlpxListener(
                EthECKey.GenerateKey(),
                new DevP2PConfig { TrustedNodeIds = new[] { configured } });

            Assert.True(listener.IsTrustedNodeId(onTheWire),
                $"configured '{configured}' did not match wire id '{onTheWire.ToHex()}'");
        }
    }
}
