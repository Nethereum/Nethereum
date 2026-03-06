using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub.ContractDefinition;
using Xunit;

namespace Nethereum.AppChain.Anchoring.IntegrationTests
{
    [Collection("Hub")]
    public class HubAnchoringTests
    {
        private readonly HubFixture _fixture;

        public HubAnchoringTests(HubFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<ulong> RegisterAndReturnChainIdAsync(ulong chainId)
        {
            var signature = _fixture.SignRegistration(chainId, HubFixture.OwnerAddress);
            var registerFunction = new RegisterAppChainFunction
            {
                ChainId = chainId,
                Sequencer = HubFixture.SequencerAddress,
                SequencerSignature = signature,
                AmountToSend = HubFixture.RegistrationFee
            };
            await _fixture.OwnerHubService.RegisterAppChainRequestAndWaitForReceiptAsync(registerFunction);
            return chainId;
        }

        [Fact]
        public async Task Anchor_AsSequencer_Succeeds()
        {
            var chainId = await RegisterAndReturnChainIdAsync(991001);

            var stateRoot = new byte[32];
            stateRoot[0] = 0xAA;
            var txRoot = new byte[32];
            txRoot[0] = 0xBB;
            var receiptRoot = new byte[32];
            receiptRoot[0] = 0xCC;

            var receipt = await _fixture.SequencerHubService.AnchorRequestAndWaitForReceiptAsync(
                chainId, 100, stateRoot, txRoot, receiptRoot, 0, Array.Empty<byte>());

            Assert.NotNull(receipt);
            Assert.NotNull(receipt.TransactionHash);

            var info = await _fixture.OwnerHubService.GetAppChainInfoQueryAsync(chainId);
            Assert.Equal((ulong)100, info.LatestBlock);
        }

        [Fact]
        public async Task Anchor_OlderBlock_Reverts()
        {
            var chainId = await RegisterAndReturnChainIdAsync(991002);

            var roots = new byte[32];
            roots[0] = 0x11;

            await _fixture.SequencerHubService.AnchorRequestAndWaitForReceiptAsync(
                chainId, 200, roots, roots, roots, 0, Array.Empty<byte>());

            await Assert.ThrowsAnyAsync<Exception>(
                () => _fixture.SequencerHubService.AnchorRequestAndWaitForReceiptAsync(
                    chainId, 100, roots, roots, roots, 0, Array.Empty<byte>()));
        }

        [Fact]
        public async Task VerifyAnchor_MatchingRoots_ReturnsTrue()
        {
            var chainId = await RegisterAndReturnChainIdAsync(991003);

            var stateRoot = new byte[32];
            stateRoot[0] = 0xDD;
            var txRoot = new byte[32];
            txRoot[0] = 0xEE;
            var receiptRoot = new byte[32];
            receiptRoot[0] = 0xFF;

            await _fixture.SequencerHubService.AnchorRequestAndWaitForReceiptAsync(
                chainId, 300, stateRoot, txRoot, receiptRoot, 0, Array.Empty<byte>());

            var verified = await _fixture.OwnerHubService.VerifyAnchorQueryAsync(
                chainId, 300, stateRoot, txRoot, receiptRoot);

            Assert.True(verified);
        }

        [Fact]
        public async Task VerifyAnchor_MismatchedRoots_ReturnsFalse()
        {
            var chainId = await RegisterAndReturnChainIdAsync(991004);

            var stateRoot = new byte[32];
            stateRoot[0] = 0x01;
            var txRoot = new byte[32];
            txRoot[0] = 0x02;
            var receiptRoot = new byte[32];
            receiptRoot[0] = 0x03;

            await _fixture.SequencerHubService.AnchorRequestAndWaitForReceiptAsync(
                chainId, 400, stateRoot, txRoot, receiptRoot, 0, Array.Empty<byte>());

            var wrongRoot = new byte[32];
            wrongRoot[0] = 0xFF;

            var verified = await _fixture.OwnerHubService.VerifyAnchorQueryAsync(
                chainId, 400, wrongRoot, txRoot, receiptRoot);

            Assert.False(verified);
        }

        [Fact]
        public async Task GetAnchor_ReturnsStoredData()
        {
            var chainId = await RegisterAndReturnChainIdAsync(991005);

            var stateRoot = new byte[32];
            stateRoot[0] = 0xAB;
            var txRoot = new byte[32];
            txRoot[0] = 0xCD;
            var receiptRoot = new byte[32];
            receiptRoot[0] = 0xEF;

            await _fixture.SequencerHubService.AnchorRequestAndWaitForReceiptAsync(
                chainId, 500, stateRoot, txRoot, receiptRoot, 0, Array.Empty<byte>());

            var anchor = await _fixture.OwnerHubService.GetAnchorQueryAsync(chainId, 500);

            Assert.Equal(stateRoot, anchor.StateRoot);
            Assert.Equal(txRoot, anchor.TxRoot);
            Assert.Equal(receiptRoot, anchor.ReceiptRoot);
            Assert.True(anchor.Timestamp > 0);
        }
    }
}
