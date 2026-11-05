using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.Documentation;
using Nethereum.Model.P2P;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PeerStatusDocExampleTests
    {
        private static byte[] Genesis(byte seed) => Enumerable.Repeat(seed, 32).ToArray();

        [Fact]
        [NethereumDocExample(DocSection.DevP2P, "devp2p-sync", "EthStatusValidator — one Status rule for every peer")]
        public void Given_AnExpectedChain_When_RemoteStatusesAreChecked_Then_TheFirstBrokenRuleIsReported()
        {
            var genesis = Genesis(0x11);
            var expected = new EthStatusExpectation
            {
                NetworkId = 1,
                GenesisHash = genesis,
                ForkBlocks = new ulong[] { 100 },
                ForkTimestamps = Array.Empty<ulong>(),
                HeadBlock = 50,
                HeadTimestamp = 0
            };
            var remote = new Eth68StatusMessage
            {
                ProtocolVersion = 68,
                NetworkId = 1,
                GenesisHash = genesis,
                ForkHash = ForkId.ComputeHash(genesis, Array.Empty<ulong>()),
                ForkNext = 100
            };

            Assert.Null(EthStatusValidator.Violation(expected, 68, remote));

            remote.NetworkId = 5;
            Assert.Contains("network 5, expected 1", EthStatusValidator.Violation(expected, 68, remote));

            remote.NetworkId = 1;
            Assert.Contains("!= negotiated 69", EthStatusValidator.Violation(expected, 69, remote));

            remote.GenesisHash = Genesis(0x22);
            Assert.Contains("genesis", EthStatusValidator.Violation(expected, 68, remote));

            remote.GenesisHash = genesis;
            remote.ForkHash = 0xDEADBEEF;
            Assert.Contains("EIP-2124", EthStatusValidator.Violation(expected, 68, remote));
        }

        [Fact]
        [NethereumDocExample(DocSection.DevP2P, "devp2p-sync", "EthStatusValidator.Decode — decode a Status and check the block range")]
        public void Given_Eth69Statuses_When_Decoded_Then_AnInvalidBlockRangeIsRefused()
        {
            var genesis = Genesis(0x11);
            var valid = Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
            {
                ProtocolVersion = 69,
                NetworkId = 1,
                GenesisHash = genesis,
                ForkHash = ForkId.ComputeHash(genesis, Array.Empty<ulong>()),
                ForkNext = 0,
                EarliestBlock = 0,
                LatestBlock = 10,
                LatestBlockHash = Genesis(0x33)
            });
            var inverted = Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
            {
                ProtocolVersion = 69,
                NetworkId = 1,
                GenesisHash = genesis,
                ForkHash = ForkId.ComputeHash(genesis, Array.Empty<ulong>()),
                ForkNext = 0,
                EarliestBlock = 20,
                LatestBlock = 10,
                LatestBlockHash = Genesis(0x33)
            });

            var decoded = EthStatusValidator.Decode(69, valid);
            Assert.Equal(10UL, decoded.LatestBlock);
            Assert.Equal(1UL, decoded.Status.NetworkId);

            Assert.Throws<InvalidOperationException>(() => EthStatusValidator.Decode(69, inverted));
        }

        [Fact]
        [NethereumDocExample(DocSection.DevP2P, "devp2p-sync", "NeedsHealAccountsCodec — persist the accounts a heal must visit")]
        public void Given_AccountsAndCodeHashesThatNeedHeal_When_EncodedAndDecoded_Then_TheyRoundTrip()
        {
            var accounts = new List<SnapSyncClient.AccountNeedingHeal>
            {
                new SnapSyncClient.AccountNeedingHeal(Genesis(0x01), Genesis(0x02)),
                new SnapSyncClient.AccountNeedingHeal(Genesis(0x03), Genesis(0x04))
            };
            var codeHashes = new List<byte[]> { Genesis(0x05), Genesis(0x06) };

            var accountBlob = NeedsHealAccountsCodec.Encode(accounts);
            var codeBlob = NeedsHealCodeCodec.Encode(codeHashes);

            var decodedAccounts = NeedsHealAccountsCodec.Decode(accountBlob);
            var decodedCode = NeedsHealCodeCodec.Decode(codeBlob);

            Assert.Equal(2, decodedAccounts.Count);
            Assert.Equal(accounts[1].AccountHash, decodedAccounts[1].AccountHash);
            Assert.Equal(accounts[1].ExpectedStorageRoot, decodedAccounts[1].ExpectedStorageRoot);
            Assert.Equal(2, decodedCode.Count);
            Assert.Equal(codeHashes[0], decodedCode[0]);
        }
    }
}
