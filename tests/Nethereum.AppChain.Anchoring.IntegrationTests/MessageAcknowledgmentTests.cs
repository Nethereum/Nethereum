using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub.ContractDefinition;
using Nethereum.AppChain.Anchoring.Messaging;
using Nethereum.Merkle;
using Nethereum.Merkle.StrategyOptions.PairingConcat;
using Nethereum.Util;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.AppChain.Anchoring.IntegrationTests
{
    [Collection("Hub")]
    public class MessageAcknowledgmentTests
    {
        private readonly HubFixture _fixture;

        public MessageAcknowledgmentTests(HubFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<ulong> RegisterChainAsync(ulong chainId)
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

            await _fixture.OwnerHubService.SetAuthorizedSenderRequestAndWaitForReceiptAsync(
                chainId, HubFixture.SenderAddress, true);

            return chainId;
        }

        private async Task SendMessagesAsync(ulong chainId, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var sendFunction = new SendMessageFunction
                {
                    SourceChainId = 1,
                    TargetChainId = chainId,
                    Target = "0x7777777777777777777777777777777777777777",
                    Data = new byte[] { (byte)(i + 1) },
                    AmountToSend = HubFixture.MessageFee
                };
                await _fixture.SenderHubService.SendMessageRequestAndWaitForReceiptAsync(sendFunction);
            }
        }

        [Fact]
        public async Task AcknowledgeMessages_AsSequencer_StoresRoot()
        {
            var chainId = await RegisterChainAsync(994001);
            await SendMessagesAsync(chainId, 3);

            var merkleRoot = new byte[32];
            merkleRoot[0] = 0xAA;
            merkleRoot[31] = 0xBB;

            var receipt = await _fixture.SequencerHubService
                .AcknowledgeMessagesRequestAndWaitForReceiptAsync(chainId, 2, merkleRoot);

            Assert.NotNull(receipt);
            Assert.NotNull(receipt.TransactionHash);

            var checkpoint = await _fixture.OwnerHubService.GetMessageRootCheckpointQueryAsync(chainId);
            Assert.Equal(merkleRoot, checkpoint.MerkleRoot);
            Assert.Equal((ulong)2, checkpoint.ProcessedUpToMessageId);
        }

        [Fact]
        public async Task AcknowledgeMessages_UpdatesLastProcessedMessageId()
        {
            var chainId = await RegisterChainAsync(994002);
            await SendMessagesAsync(chainId, 5);

            var merkleRoot = new byte[32];
            merkleRoot[0] = 0xCC;

            await _fixture.SequencerHubService
                .AcknowledgeMessagesRequestAndWaitForReceiptAsync(chainId, 3, merkleRoot);

            var info = await _fixture.OwnerHubService.GetAppChainInfoQueryAsync(chainId);
            Assert.Equal((ulong)3, info.LastProcessedMessageId);

            var pending = await _fixture.OwnerHubService.PendingMessageCountQueryAsync(chainId);
            Assert.Equal((ulong)2, pending);
        }

        [Fact]
        public async Task AcknowledgeMessages_NonSequencer_Reverts()
        {
            var chainId = await RegisterChainAsync(994003);
            await SendMessagesAsync(chainId, 2);

            var merkleRoot = new byte[32];

            await Assert.ThrowsAnyAsync<Exception>(
                () => _fixture.OwnerHubService
                    .AcknowledgeMessagesRequestAndWaitForReceiptAsync(chainId, 1, merkleRoot));
        }

        [Fact]
        public async Task AcknowledgeMessages_FutureMessageId_Reverts()
        {
            var chainId = await RegisterChainAsync(994004);
            await SendMessagesAsync(chainId, 2);

            var merkleRoot = new byte[32];

            await Assert.ThrowsAnyAsync<Exception>(
                () => _fixture.SequencerHubService
                    .AcknowledgeMessagesRequestAndWaitForReceiptAsync(chainId, 10, merkleRoot));
        }

        [Fact]
        public async Task GetMessageRootCheckpoint_NoAck_ReturnsZero()
        {
            var chainId = await RegisterChainAsync(994005);

            var checkpoint = await _fixture.OwnerHubService.GetMessageRootCheckpointQueryAsync(chainId);

            Assert.True(checkpoint.MerkleRoot.All(b => b == 0));
            Assert.Equal((ulong)0, checkpoint.ProcessedUpToMessageId);
        }

        [Fact]
        public async Task MessageRootCheckpoints_DirectMapping_ReturnsStoredValue()
        {
            var chainId = await RegisterChainAsync(994006);
            await SendMessagesAsync(chainId, 3);

            var merkleRoot = new byte[32];
            merkleRoot[0] = 0xDD;
            merkleRoot[15] = 0xEE;

            await _fixture.SequencerHubService
                .AcknowledgeMessagesRequestAndWaitForReceiptAsync(chainId, 2, merkleRoot);

            var checkpoint = await _fixture.OwnerHubService.MessageRootCheckpointsQueryAsync(chainId);
            Assert.Equal(merkleRoot, checkpoint.MerkleRoot);
            Assert.Equal((ulong)2, checkpoint.ProcessedUpToMessageId);
        }

        [Fact]
        public async Task VerifyMessageInclusion_ValidProof_ReturnsTrue()
        {
            var chainId = await RegisterChainAsync(994007);
            await SendMessagesAsync(chainId, 3);

            var accumulator = new MessageMerkleAccumulator();
            var hashProvider = new Sha3KeccackHashProvider();

            var fixedTxHash = new byte[32];
            fixedTxHash[0] = 0x11;
            fixedTxHash[1] = 0x22;

            for (int i = 0; i < 3; i++)
            {
                var dataHash = Sha3Keccack.Current.CalculateHash(new byte[] { (byte)(i + 1) });
                var leaf = new MessageLeaf
                {
                    SourceChainId = 1,
                    MessageId = (ulong)(i + 1),
                    AppChainTxHash = fixedTxHash,
                    Success = true,
                    DataHash = dataHash
                };
                accumulator.AppendLeaf(1, leaf);
            }

            var merkleRoot = accumulator.GetRoot(1);

            await _fixture.SequencerHubService
                .AcknowledgeMessagesRequestAndWaitForReceiptAsync(chainId, 3, merkleRoot);

            var checkpoint = await _fixture.OwnerHubService.GetMessageRootCheckpointQueryAsync(chainId);
            Assert.Equal(merkleRoot, checkpoint.MerkleRoot);
            Assert.Equal((ulong)3, checkpoint.ProcessedUpToMessageId);

            var proofForLeaf0 = accumulator.GenerateProof(1, 0);
            var dataHash0 = Sha3Keccack.Current.CalculateHash(new byte[] { 0x01 });

            var verified = await _fixture.OwnerHubService.VerifyMessageInclusionQueryAsync(
                chainId,
                proofForLeaf0.ProofNodes,
                1,
                1,
                fixedTxHash,
                true,
                dataHash0);

            Assert.True(verified, "On-chain verification of C#-generated Merkle proof failed");
        }

        [Fact]
        public async Task VerifyMessageInclusion_AllLeaves_Verify()
        {
            var chainId = await RegisterChainAsync(994008);
            await SendMessagesAsync(chainId, 5);

            var accumulator = new MessageMerkleAccumulator();

            var txHash = new byte[32];
            txHash[0] = 0x33;

            for (int i = 0; i < 5; i++)
            {
                var dataHash = Sha3Keccack.Current.CalculateHash(new byte[] { (byte)(i + 1) });
                var leaf = new MessageLeaf
                {
                    SourceChainId = 1,
                    MessageId = (ulong)(i + 1),
                    AppChainTxHash = txHash,
                    Success = true,
                    DataHash = dataHash
                };
                accumulator.AppendLeaf(1, leaf);
            }

            var merkleRoot = accumulator.GetRoot(1);
            await _fixture.SequencerHubService
                .AcknowledgeMessagesRequestAndWaitForReceiptAsync(chainId, 5, merkleRoot);

            for (int i = 0; i < 5; i++)
            {
                var proof = accumulator.GenerateProof(1, i);
                var dataHash = Sha3Keccack.Current.CalculateHash(new byte[] { (byte)(i + 1) });

                var verified = await _fixture.OwnerHubService.VerifyMessageInclusionQueryAsync(
                    chainId,
                    proof.ProofNodes,
                    1,
                    (ulong)(i + 1),
                    txHash,
                    true,
                    dataHash);

                Assert.True(verified, $"On-chain verification failed for leaf {i}");
            }
        }

        [Fact]
        public async Task VerifyMessageInclusion_WrongData_ReturnsFalse()
        {
            var chainId = await RegisterChainAsync(994009);
            await SendMessagesAsync(chainId, 2);

            var accumulator = new MessageMerkleAccumulator();

            var txHash = new byte[32];
            txHash[0] = 0x44;

            for (int i = 0; i < 2; i++)
            {
                var dataHash = Sha3Keccack.Current.CalculateHash(new byte[] { (byte)(i + 1) });
                var leaf = new MessageLeaf
                {
                    SourceChainId = 1,
                    MessageId = (ulong)(i + 1),
                    AppChainTxHash = txHash,
                    Success = true,
                    DataHash = dataHash
                };
                accumulator.AppendLeaf(1, leaf);
            }

            var merkleRoot = accumulator.GetRoot(1);
            await _fixture.SequencerHubService
                .AcknowledgeMessagesRequestAndWaitForReceiptAsync(chainId, 2, merkleRoot);

            var proof = accumulator.GenerateProof(1, 0);
            var wrongDataHash = new byte[32];
            wrongDataHash[0] = 0xFF;

            var verified = await _fixture.OwnerHubService.VerifyMessageInclusionQueryAsync(
                chainId,
                proof.ProofNodes,
                1,
                1,
                txHash,
                true,
                wrongDataHash);

            Assert.False(verified, "Verification should fail with wrong data hash");
        }

        [Fact]
        public async Task VerifyMessageInclusion_FailedMessage_VerifiesCorrectly()
        {
            var chainId = await RegisterChainAsync(994010);
            await SendMessagesAsync(chainId, 2);

            var accumulator = new MessageMerkleAccumulator();
            var txHash = new byte[32];
            txHash[0] = 0x55;

            var dataHash1 = Sha3Keccack.Current.CalculateHash(new byte[] { 0x01 });
            accumulator.AppendLeaf(1, new MessageLeaf
            {
                SourceChainId = 1, MessageId = 1, AppChainTxHash = txHash, Success = true, DataHash = dataHash1
            });

            var dataHash2 = Sha3Keccack.Current.CalculateHash(new byte[] { 0x02 });
            accumulator.AppendLeaf(1, new MessageLeaf
            {
                SourceChainId = 1, MessageId = 2, AppChainTxHash = txHash, Success = false, DataHash = dataHash2
            });

            var merkleRoot = accumulator.GetRoot(1);
            await _fixture.SequencerHubService
                .AcknowledgeMessagesRequestAndWaitForReceiptAsync(chainId, 2, merkleRoot);

            var proof = accumulator.GenerateProof(1, 1);
            var verified = await _fixture.OwnerHubService.VerifyMessageInclusionQueryAsync(
                chainId, proof.ProofNodes, 1, 2, txHash, false, dataHash2);

            Assert.True(verified, "Failed message should verify with success=false");

            var wrongVerified = await _fixture.OwnerHubService.VerifyMessageInclusionQueryAsync(
                chainId, proof.ProofNodes, 1, 2, txHash, true, dataHash2);

            Assert.False(wrongVerified, "Should fail when success flag doesn't match");
        }

        [Fact]
        public async Task MultiBatch_InclusionAndNonInclusion_AfterMultipleAcknowledgments()
        {
            var chainId = await RegisterChainAsync(994012);
            await SendMessagesAsync(chainId, 10);

            var accumulator = new MessageMerkleAccumulator();
            var txHash = new byte[32];
            txHash[0] = 0x66;

            // === BATCH 1: process messages 1-3 ===
            for (int i = 0; i < 3; i++)
            {
                var dataHash = Sha3Keccack.Current.CalculateHash(new byte[] { (byte)(i + 1) });
                accumulator.AppendLeaf(1, new MessageLeaf
                {
                    SourceChainId = 1,
                    MessageId = (ulong)(i + 1),
                    AppChainTxHash = txHash,
                    Success = true,
                    DataHash = dataHash
                });
            }

            var root1 = accumulator.GetRoot(1).ToArray();
            var proofMsg1_batch1 = accumulator.GenerateProof(1, 0);

            await _fixture.SequencerHubService
                .AcknowledgeMessagesRequestAndWaitForReceiptAsync(chainId, 3, root1);

            var cp1 = await _fixture.OwnerHubService.GetMessageRootCheckpointQueryAsync(chainId);
            Assert.Equal((ulong)3, cp1.ProcessedUpToMessageId);
            Assert.Equal(root1, cp1.MerkleRoot);

            var dataHash1 = Sha3Keccack.Current.CalculateHash(new byte[] { 0x01 });
            var verifiedBatch1 = await _fixture.OwnerHubService.VerifyMessageInclusionQueryAsync(
                chainId, proofMsg1_batch1.ProofNodes, 1, 1, txHash, true, dataHash1);
            Assert.True(verifiedBatch1, "Message 1 should verify against batch 1 root");

            // === BATCH 2: process messages 4-7 ===
            for (int i = 3; i < 7; i++)
            {
                var dataHash = Sha3Keccack.Current.CalculateHash(new byte[] { (byte)(i + 1) });
                accumulator.AppendLeaf(1, new MessageLeaf
                {
                    SourceChainId = 1,
                    MessageId = (ulong)(i + 1),
                    AppChainTxHash = txHash,
                    Success = i != 5,
                    DataHash = dataHash
                });
            }

            var root2 = accumulator.GetRoot(1).ToArray();
            Assert.False(root1.SequenceEqual(root2), "Root must change after batch 2");

            await _fixture.SequencerHubService
                .AcknowledgeMessagesRequestAndWaitForReceiptAsync(chainId, 7, root2);

            var cp2 = await _fixture.OwnerHubService.GetMessageRootCheckpointQueryAsync(chainId);
            Assert.Equal((ulong)7, cp2.ProcessedUpToMessageId);
            Assert.Equal(root2, cp2.MerkleRoot);

            // Old proof from batch 1 must FAIL against new root
            var staleVerified = await _fixture.OwnerHubService.VerifyMessageInclusionQueryAsync(
                chainId, proofMsg1_batch1.ProofNodes, 1, 1, txHash, true, dataHash1);
            Assert.False(staleVerified, "Stale proof from batch 1 root must not verify against batch 2 root");

            // Message 1 (from batch 1) with FRESH proof must still verify against new root
            var freshProofMsg1 = accumulator.GenerateProof(1, 0);
            var freshVerifiedMsg1 = await _fixture.OwnerHubService.VerifyMessageInclusionQueryAsync(
                chainId, freshProofMsg1.ProofNodes, 1, 1, txHash, true, dataHash1);
            Assert.True(freshVerifiedMsg1, "Message 1 must still verify with fresh proof against batch 2 root");

            // Message 5 (from batch 2) verifies
            var dataHash5 = Sha3Keccack.Current.CalculateHash(new byte[] { 0x05 });
            var proofMsg5 = accumulator.GenerateProof(1, 4);
            var verifiedMsg5 = await _fixture.OwnerHubService.VerifyMessageInclusionQueryAsync(
                chainId, proofMsg5.ProofNodes, 1, 5, txHash, true, dataHash5);
            Assert.True(verifiedMsg5, "Message 5 from batch 2 must verify");

            // Message 6 was processed with success=false (i==5 above)
            var dataHash6 = Sha3Keccack.Current.CalculateHash(new byte[] { 0x06 });
            var proofMsg6 = accumulator.GenerateProof(1, 5);
            var verifiedMsg6Correct = await _fixture.OwnerHubService.VerifyMessageInclusionQueryAsync(
                chainId, proofMsg6.ProofNodes, 1, 6, txHash, false, dataHash6);
            Assert.True(verifiedMsg6Correct, "Message 6 (failed) must verify with success=false");

            var verifiedMsg6Wrong = await _fixture.OwnerHubService.VerifyMessageInclusionQueryAsync(
                chainId, proofMsg6.ProofNodes, 1, 6, txHash, true, dataHash6);
            Assert.False(verifiedMsg6Wrong, "Message 6 must NOT verify with success=true (it failed)");

            // === BATCH 3: process messages 8-10 ===
            for (int i = 7; i < 10; i++)
            {
                var dataHash = Sha3Keccack.Current.CalculateHash(new byte[] { (byte)(i + 1) });
                accumulator.AppendLeaf(1, new MessageLeaf
                {
                    SourceChainId = 1,
                    MessageId = (ulong)(i + 1),
                    AppChainTxHash = txHash,
                    Success = true,
                    DataHash = dataHash
                });
            }

            var root3 = accumulator.GetRoot(1).ToArray();
            Assert.False(root2.SequenceEqual(root3), "Root must change after batch 3");

            await _fixture.SequencerHubService
                .AcknowledgeMessagesRequestAndWaitForReceiptAsync(chainId, 10, root3);

            var cp3 = await _fixture.OwnerHubService.GetMessageRootCheckpointQueryAsync(chainId);
            Assert.Equal((ulong)10, cp3.ProcessedUpToMessageId);
            Assert.Equal(root3, cp3.MerkleRoot);

            // Verify ALL 10 messages against the final root
            for (int i = 0; i < 10; i++)
            {
                var dataHash = Sha3Keccack.Current.CalculateHash(new byte[] { (byte)(i + 1) });
                bool expectedSuccess = i != 5;
                var proof = accumulator.GenerateProof(1, i);

                var verified = await _fixture.OwnerHubService.VerifyMessageInclusionQueryAsync(
                    chainId, proof.ProofNodes, 1, (ulong)(i + 1), txHash, expectedSuccess, dataHash);
                Assert.True(verified, $"Message {i + 1} must verify against final root (batch {(i < 3 ? 1 : i < 7 ? 2 : 3)})");
            }

            // Fabricated message (never processed) must NOT verify
            var fakeDataHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0xFF });
            var fakeProof = accumulator.GenerateProof(1, 0);
            var fakeVerified = await _fixture.OwnerHubService.VerifyMessageInclusionQueryAsync(
                chainId, fakeProof.ProofNodes, 1, 999, txHash, true, fakeDataHash);
            Assert.False(fakeVerified, "Fabricated message must not verify");

            // Correct proof but wrong messageId must NOT verify
            var proofMsg3 = accumulator.GenerateProof(1, 2);
            var dataHash3 = Sha3Keccack.Current.CalculateHash(new byte[] { 0x03 });
            var wrongIdVerified = await _fixture.OwnerHubService.VerifyMessageInclusionQueryAsync(
                chainId, proofMsg3.ProofNodes, 1, 42, txHash, true, dataHash3);
            Assert.False(wrongIdVerified, "Correct proof but wrong messageId must not verify");

            // Correct leaf data but proof from wrong index must NOT verify
            var proofFromWrongIndex = accumulator.GenerateProof(1, 9);
            var wrongIndexVerified = await _fixture.OwnerHubService.VerifyMessageInclusionQueryAsync(
                chainId, proofFromWrongIndex.ProofNodes, 1, 3, txHash, true, dataHash3);
            Assert.False(wrongIndexVerified, "Proof from wrong leaf index must not verify for message 3");

        }

        [Fact]
        public async Task E2E_SendPollProcessAcknowledgeVerify()
        {
            var chainId = await RegisterChainAsync(994011);

            var sendFunction = new SendMessageFunction
            {
                SourceChainId = 1,
                TargetChainId = chainId,
                Target = "0x8888888888888888888888888888888888888888",
                Data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF },
                AmountToSend = HubFixture.MessageFee
            };
            await _fixture.SenderHubService.SendMessageRequestAndWaitForReceiptAsync(sendFunction);

            var hubMessageService = new HubMessageService(
                _fixture.OwnerWeb3, _fixture.HubContractAddress, chainId);
            var polled = await hubMessageService.GetPendingMessagesAsync(0, 100);

            Assert.Single(polled);
            Assert.Equal((ulong)1, polled[0].MessageId);
            Assert.Equal("0x8888888888888888888888888888888888888888".ToLowerInvariant(),
                polled[0].Target.ToLowerInvariant());

            var accumulator = new MessageMerkleAccumulator();
            var processor = new MessageProcessor(accumulator, executor: msg =>
            {
                var txHash = new byte[32];
                txHash[0] = 0xAB;
                txHash[1] = 0xCD;
                return Task.FromResult(new MessageExecutionResult
                {
                    TxHash = txHash,
                    Success = true,
                    GasUsed = 21000
                });
            });

            var batchResult = await processor.ProcessBatchAsync(polled);

            Assert.Equal(1, batchResult.ProcessedCount);
            Assert.Equal(0, batchResult.FailedCount);
            Assert.True(batchResult.Results[0].Success);

            var merkleRoot = accumulator.GetRoot(1);
            Assert.True(merkleRoot.Length > 0);

            await _fixture.SequencerHubService
                .AcknowledgeMessagesRequestAndWaitForReceiptAsync(chainId, 1, merkleRoot);

            var checkpoint = await _fixture.OwnerHubService.GetMessageRootCheckpointQueryAsync(chainId);
            Assert.Equal(merkleRoot, checkpoint.MerkleRoot);
            Assert.Equal((ulong)1, checkpoint.ProcessedUpToMessageId);

            var info = await _fixture.OwnerHubService.GetAppChainInfoQueryAsync(chainId);
            Assert.Equal((ulong)1, info.LastProcessedMessageId);

            var proof = accumulator.GenerateProof(1, 0);
            var execTxHash = new byte[32];
            execTxHash[0] = 0xAB;
            execTxHash[1] = 0xCD;
            var dataHash = Sha3Keccack.Current.CalculateHash(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

            var verified = await _fixture.OwnerHubService.VerifyMessageInclusionQueryAsync(
                chainId, proof.ProofNodes, 1, 1, execTxHash, true, dataHash);

            Assert.True(verified, "E2E: User verification of message inclusion failed");
        }
    }
}
