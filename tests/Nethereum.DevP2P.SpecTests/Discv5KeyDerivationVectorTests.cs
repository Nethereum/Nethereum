using Nethereum.DevP2P.Discv5;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.SpecTests
{
    /// <summary>
    /// discv5 wire test vectors from go-ethereum's
    /// <c>p2p/discover/v5wire/crypto_test.go</c> — the same ECDH / KDF /
    /// id-signature vectors that Geth validates against. Pinning these here
    /// catches regressions in the key-derivation maths independently of the
    /// network conformance harness.
    /// </summary>
    public class Discv5KeyDerivationVectorTests
    {
        [Fact]
        public void Kdf_TestVector_ProducesGethSessionKeys()
        {
            // From TestVector_KDF in v5wire/crypto_test.go.
            // ephKey is the static-key fixture also used by the ECDH test;
            // testKeyA/testKeyB come from encoding_test.go.
            var ephKey = new EthECKey(
                "0xfb757dc581730490a1d7a00deea65e9b1936924caaea8f44d476014856b68736".HexToByteArray(),
                isPrivate: true);
            var testKeyB = new EthECKey(
                "0x66fb62bfbd66b9177a138c1e5cddbe4f7c30c343e94e68df8769459cb1cde628".HexToByteArray(),
                isPrivate: true);
            // node-id-A = keccak256(testKeyA pubkey x||y), node-id-B = keccak256(testKeyB pubkey x||y)
            var testKeyA = new EthECKey(
                "0xeef77acb6c6a6eebc5b363a475ac583ec7eccdb42b6481424c60f59aa326547f".HexToByteArray(),
                isPrivate: true);
            var nodeAId = Discv5Crypto.ComputeNodeId(testKeyA.GetPubKeyNoPrefix());
            var nodeBId = Discv5Crypto.ComputeNodeId(testKeyB.GetPubKeyNoPrefix());

            var cdata = ("0x000000000000000000000000000000006469736376350001010102030405060708"
                       + "090a0b0c00180102030405060708090a0b0c0d0e0f100000000000000000").HexToByteArray();

            // Run the full pipeline: ECDH(ephKey, testKeyB.pub) then HKDF.
            var testKeyBPubCompressed = testKeyB.GetPubKey(true);
            var sharedSecret = Discv5Crypto.EcdhCompressed(ephKey, testKeyBPubCompressed);
            var (initiatorKey, recipientKey) = Discv5KeyDerivation.DeriveSessionKeys(sharedSecret, cdata, nodeAId, nodeBId);

            Assert.Equal("0xdccc82d81bd610f4f76d3ebe97a40571", initiatorKey.ToHex(true));
            Assert.Equal("0xac74bb8773749920b0d3a8881c173ec5", recipientKey.ToHex(true));
        }

        [Fact]
        public void Ecdh_TestVector_ProducesCompressedSharedSecret()
        {
            // From TestVector_ECDH in v5wire/crypto_test.go
            var staticKeyBytes = "0xfb757dc581730490a1d7a00deea65e9b1936924caaea8f44d476014856b68736".HexToByteArray();
            var pubKeyCompressed = "0x039961e4c2356d61bedb83052c115d311acb3a96f5777296dcf297351130266231".HexToByteArray();
            var expected = "0x033b11a2a1f214567e1537ce5e509ffd9b21373247f2a3ff6841f4976f53165e7e".HexToByteArray();

            var staticKey = new EthECKey(staticKeyBytes, isPrivate: true);
            var result = Discv5Crypto.EcdhCompressed(staticKey, pubKeyCompressed);

            Assert.Equal(expected.ToHex(), result.ToHex());
        }
    }
}
