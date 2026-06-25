using System.Security.Cryptography;
using Nethereum.DevP2P.Crypto;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Crypto
{
    public class RlpxSecretsTests
    {
        [Fact]
        public void DeriveSecrets_ProducesCorrectKeyLengths()
        {
            var initiatorEph = EthECKey.GenerateKey();
            var recipientEph = EthECKey.GenerateKey();
            var initiatorNonce = new byte[32];
            var recipientNonce = new byte[32];
            RandomNumberGenerator.Fill(initiatorNonce);
            RandomNumberGenerator.Fill(recipientNonce);
            var authPacket = new byte[200];
            var ackPacket = new byte[200];

            var secrets = RlpxSecrets.Derive(
                initiatorEph, recipientEph.GetPubKeyNoPrefix(),
                initiatorNonce, recipientNonce,
                authPacket, ackPacket, isInitiator: true);

            Assert.Equal(32, secrets.AesSecret.Length);
            Assert.Equal(32, secrets.MacSecret.Length);
            Assert.NotNull(secrets.EgressMac);
            Assert.NotNull(secrets.IngressMac);
        }

        [Fact]
        public void DeriveSecrets_BothSides_ProduceSameAesAndMacKeys()
        {
            var initiatorEph = EthECKey.GenerateKey();
            var recipientEph = EthECKey.GenerateKey();
            var initiatorNonce = new byte[32];
            var recipientNonce = new byte[32];
            RandomNumberGenerator.Fill(initiatorNonce);
            RandomNumberGenerator.Fill(recipientNonce);
            var authPacket = new byte[200];
            var ackPacket = new byte[200];

            var initiatorSecrets = RlpxSecrets.Derive(
                initiatorEph, recipientEph.GetPubKeyNoPrefix(),
                initiatorNonce, recipientNonce,
                authPacket, ackPacket, isInitiator: true);

            var recipientSecrets = RlpxSecrets.Derive(
                recipientEph, initiatorEph.GetPubKeyNoPrefix(),
                initiatorNonce, recipientNonce,
                authPacket, ackPacket, isInitiator: false);

            Assert.Equal(initiatorSecrets.AesSecret, recipientSecrets.AesSecret);
            Assert.Equal(initiatorSecrets.MacSecret, recipientSecrets.MacSecret);
        }

        [Fact]
        public void DeriveSecrets_Eip8TestVector_MatchesExpected()
        {
            var keyA = new EthECKey("49a7b37aa6f6645917e7b807e9d1c00d4fa71f18343b0d4122a4d2df64dd6fee".HexToByteArray(), true);
            var keyB = new EthECKey("b71c71a67e1177ad4e901695e1b4b9ee17ae16c6668d313eac2f96dbcda3f291".HexToByteArray(), true);
            var ephA = new EthECKey("869d6ecf5211f1cc60418a13b9d870b22959d0c16f02bec714c960dd2298a32d".HexToByteArray(), true);
            var ephB = new EthECKey("e238eb8e04fee6511ab04c6dd3c89ce097b11f25d584863ac2b6d5b35b1847e4".HexToByteArray(), true);
            var nonceA = "7e968bba13b6c50e2c4cd7f241cc0d64d1ac25c7f5952df231ac6a2bda8ee5d6".HexToByteArray();
            var nonceB = "559aead08264d5795d3909718cdd05abd49572e84fe55590eef31a88a08fdffd".HexToByteArray();

            var auth2 = ("01b304ab7578555167be8154d5cc456f567d5ba302662433674222360f08d5f1534499d3678b513b"
                + "0fca474f3a514b18e75683032eb63fccb16c156dc6eb2c0b1593f0d84ac74f6e475f1b8d56116b84"
                + "9634a8c458705bf83a626ea0384d4d7341aae591fae42ce6bd5c850bfe0b999a694a49bbbaf3ef6c"
                + "da61110601d3b4c02ab6c30437257a6e0117792631a4b47c1d52fc0f8f89caadeb7d02770bf999cc"
                + "147d2df3b62e1ffb2c9d8c125a3984865356266bca11ce7d3a688663a51d82defaa8aad69da39ab6"
                + "d5470e81ec5f2a7a47fb865ff7cca21516f9299a07b1bc63ba56c7a1a892112841ca44b6e0034dee"
                + "70c9adabc15d76a54f443593fafdc3b27af8059703f88928e199cb122362a4b35f62386da7caad09"
                + "c001edaeb5f8a06d2b26fb6cb93c52a9fca51853b68193916982358fe1e5369e249875bb8d0d0ec3"
                + "6f917bc5e1eafd5896d46bd61ff23f1a863a8a8dcd54c7b109b771c8e61ec9c8908c733c0263440e"
                + "2aa067241aaa433f0bb053c7b31a838504b148f570c0ad62837129e547678c5190341e4f1693956c"
                + "3bf7678318e2d5b5340c9e488eefea198576344afbdf66db5f51204a6961a63ce072c8926c").HexToByteArray();

            var ack2 = ("01ea0451958701280a56482929d3b0757da8f7fbe5286784beead59d95089c217c9b917788989470"
                + "b0e330cc6e4fb383c0340ed85fab836ec9fb8a49672712aeabbdfd1e837c1ff4cace34311cd7f4de"
                + "05d59279e3524ab26ef753a0095637ac88f2b499b9914b5f64e143eae548a1066e14cd2f4bd7f814"
                + "c4652f11b254f8a2d0191e2f5546fae6055694aed14d906df79ad3b407d94692694e259191cde171"
                + "ad542fc588fa2b7333313d82a9f887332f1dfc36cea03f831cb9a23fea05b33deb999e85489e645f"
                + "6aab1872475d488d7bd6c7c120caf28dbfc5d6833888155ed69d34dbdc39c1f299be1057810f34fb"
                + "e754d021bfca14dc989753d61c413d261934e1a9c67ee060a25eefb54e81a4d14baff922180c395d"
                + "3f998d70f46f6b58306f969627ae364497e73fc27f6d17ae45a413d322cb8814276be6ddd13b885b"
                + "201b943213656cde498fa0e9ddc8e0b8f8a53824fbd82254f3e2c17e8eaea009c38b4aa0a3f306e8"
                + "797db43c25d68e86f262e564086f59a2fc60511c42abfb3057c247a8a8fe4fb3ccbadde17514b7ac"
                + "8000cdb6a912778426260c47f38919a91f25f4b5ffb455d6aaaf150f7e5529c100ce62d6d92826a7"
                + "1778d809bdf60232ae21ce8a437eca8223f45ac37f6487452ce626f549b3b5fdee26afd2072e4bc7"
                + "5833c2464c805246155289f4").HexToByteArray();

            // Derive on the recipient (B) side, matching go-ethereum test
            var secrets = RlpxSecrets.Derive(
                ephB, ephA.GetPubKeyNoPrefix(),
                nonceA, nonceB,
                auth2, ack2, isInitiator: false);

            Assert.Equal(
                "80e8632c05fed6fc2a13b0f8d31a3cf645366239170ea067065aba8e28bac487",
                secrets.AesSecret.ToHex());
            Assert.Equal(
                "2ea74ec5dae199227dff1af715362700e989d889d7a493cb0639691efb8e5f98",
                secrets.MacSecret.ToHex());

            // Verify ingress MAC state by hashing "foo"
            var ingressMacCopy = secrets.IngressMac.Clone();
            ingressMacCopy.Update(System.Text.Encoding.ASCII.GetBytes("foo"));
            var digest = ingressMacCopy.DigestFirst16();
            var fullDigest = new byte[32];
            var copyDigest = new Org.BouncyCastle.Crypto.Digests.KeccakDigest(256);
            // We need the full 32-byte digest, not just first 16
            // Reconstruct: clone ingress, update "foo", get full hash
            var ingressMacCopy2 = secrets.IngressMac.Clone();
            ingressMacCopy2.Update(System.Text.Encoding.ASCII.GetBytes("foo"));
            // DigestFirst16 only gives 16 bytes. For this test vector we need full 32.
            // The test vector says ingress-MAC("foo") = 0c7ec6...
            // Let's verify the first 16 bytes match the first 16 of the expected hash
            Assert.Equal(
                "0c7ec6340062cc46f5e9f1e3cf86f8c8",
                digest.ToHex());
        }
    }
}
