using System.Net;
using System.Text;
using Nethereum.DevP2P.Discv5;
using Nethereum.Model.Enr;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv5
{
    public class Discv5EthForkIdFilterTests
    {
        private static EnrRecord BuildEnrWithEth(byte[] ethValue)
        {
            var key = EthECKey.GenerateKey();
            var enr = new EnrRecord { Sequence = 1 };
            enr.Pairs["id"] = Encoding.ASCII.GetBytes("v4");
            enr.Pairs["ip"] = new byte[] { 203, 0, 113, 7 };
            enr.Pairs["tcp"] = new byte[] { 0x76, 0x5F };
            enr.Pairs["secp256k1"] = key.GetPubKey(compresseed: true);
            if (ethValue != null) enr.Pairs["eth"] = ethValue;
            EnrRecordSigner.Sign(enr, key);
            return enr;
        }

        [Fact]
        public void Discv5_AdmitsCompatibleForkPeer()
        {
            var ethValue = EnrForkIdEntry.Encode(0xfc64ec04, 1_150_000UL);
            var enr = BuildEnrWithEth(ethValue);

            var admitted = Discv5PeerDiscoveryService.AdmitsForkId(enr, ethBytes =>
            {
                EnrForkIdEntry.Decode(ethBytes, out var hash, out _);
                return hash == 0xfc64ec04u;
            });

            Assert.True(admitted);
        }

        [Fact]
        public void Discv5_RejectsIncompatibleForkPeer()
        {
            var ethValue = EnrForkIdEntry.Encode(0xdeadbeef, 1_150_000UL);
            var enr = BuildEnrWithEth(ethValue);

            var admitted = Discv5PeerDiscoveryService.AdmitsForkId(enr, ethBytes =>
            {
                EnrForkIdEntry.Decode(ethBytes, out var hash, out _);
                return hash == 0xfc64ec04u;
            });

            Assert.False(admitted);
        }

        [Fact]
        public void Discv5_AdmitsPeerWithNoEthEntry()
        {
            var enr = BuildEnrWithEth(null);

            var admitted = Discv5PeerDiscoveryService.AdmitsForkId(enr, _ => false);

            Assert.True(admitted);
        }

        [Fact]
        public void Discv5_NoFilter_AdmitsEveryone()
        {
            var ethValue = EnrForkIdEntry.Encode(0xdeadbeef, 1_150_000UL);
            var enr = BuildEnrWithEth(ethValue);

            var admitted = Discv5PeerDiscoveryService.AdmitsForkId(enr, ethForkIdFilter: null);

            Assert.True(admitted);
        }

        [Fact]
        public void Discv5_MalformedEthEntry_FilterThrows_StillAdmits()
        {
            var enr = BuildEnrWithEth(new byte[] { 0xFF, 0xFF });

            var admitted = Discv5PeerDiscoveryService.AdmitsForkId(enr, ethBytes =>
            {
                EnrForkIdEntry.Decode(ethBytes, out _, out _);
                return false;
            });

            Assert.True(admitted);
        }
    }
}
