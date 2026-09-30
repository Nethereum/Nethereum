using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Modules.MultiGuardian
{
    public class MultiGuardianPersonalSignTests
    {
        private static readonly byte[] Message = Encoding.UTF8.GetBytes("recover my account");

        [Fact]
        public async Task SendRequestAsync_SingleGuardianAtThresholdOne_MatchesEthPersonalOfflineSignByteForByte()
        {
            var guardian = EthECKey.GenerateKey();

            var multiGuardianSignature = await new MultiGuardianPersonalSign(new[] { guardian }, 1)
                .SendRequestAsync(Message);
            var singleKeySignature = await new EthPersonalOfflineSign(guardian).SendRequestAsync(Message);

            Assert.Equal(singleKeySignature, multiGuardianSignature, ignoreCase: true);
        }

        [Fact]
        public async Task SendRequestAsync_BlobLength_IsExactlyThresholdTimes65()
        {
            var guardians = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey(), EthECKey.GenerateKey() };
            var signer = new MultiGuardianPersonalSign(guardians, 2);

            var blob = (await signer.SendRequestAsync(Message)).HexToByteArray();

            Assert.Equal(2 * 65, blob.Length);
        }

        [Fact]
        public async Task SendRequestAsync_SlotsRecoverToGuardians_OverTheEip191WrappedMessage()
        {
            var guardians = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey() };
            var threshold = 2;
            var signer = new MultiGuardianPersonalSign(guardians, threshold);

            var blob = (await signer.SendRequestAsync(Message)).HexToByteArray();

            var expectedOrder = guardians
                .Select(g => g.GetPublicAddress())
                .OrderBy(a => a.HexToBigInteger(false))
                .ToArray();

            var wrappedHash = new EthereumMessageSigner().HashPrefixedMessage(Message);
            for (var i = 0; i < threshold; i++)
            {
                var slot = blob.Skip(i * 65).Take(65).ToArray();
                var recovered = new MessageSigner().EcRecover(wrappedHash, slot.ToHex(true));
                Assert.Equal(expectedOrder[i], recovered, ignoreCase: true);
            }
        }

        [Fact]
        public async Task SendRequestAsync_NullValue_Throws()
        {
            var signer = new MultiGuardianPersonalSign(new[] { EthECKey.GenerateKey() }, 1);
            await Assert.ThrowsAsync<ArgumentNullException>(() => signer.SendRequestAsync((byte[])null!));
        }

        [Fact]
        public void Constructor_NullGuardians_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MultiGuardianPersonalSign(null!, 1));
        }

        [Fact]
        public void Constructor_FewerGuardiansThanThreshold_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new MultiGuardianPersonalSign(new[] { EthECKey.GenerateKey() }, 2));
        }

        [Fact]
        public void BuildRequest_ThrowsNotImplemented()
        {
            var signer = new MultiGuardianPersonalSign(new[] { EthECKey.GenerateKey() }, 1);
            Assert.Throws<NotImplementedException>(() => signer.BuildRequest(Message));
        }
    }
}
