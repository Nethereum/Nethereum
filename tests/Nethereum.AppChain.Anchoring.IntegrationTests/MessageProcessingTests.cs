using Nethereum.AppChain.Anchoring.Messaging;
using Nethereum.Merkle;
using Nethereum.Merkle.StrategyOptions.PairingConcat;
using Nethereum.Util;
using Nethereum.Util.ByteArrayConvertors;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.AppChain.Anchoring.IntegrationTests
{
    public class MessageProcessingTests
    {
        [Fact]
        public void MessageLeaf_ComputeHash_IsDeterministic()
        {
            var hash1 = MessageLeaf.ComputeHash(1, 42, new byte[32], true, new byte[32]);
            var hash2 = MessageLeaf.ComputeHash(1, 42, new byte[32], true, new byte[32]);

            Assert.Equal(hash1, hash2);
            Assert.Equal(32, hash1.Length);
        }

        [Fact]
        public void MessageLeaf_DifferentInputs_ProduceDifferentHashes()
        {
            var hash1 = MessageLeaf.ComputeHash(1, 42, new byte[32], true, new byte[32]);
            var hash2 = MessageLeaf.ComputeHash(1, 43, new byte[32], true, new byte[32]);
            var hash3 = MessageLeaf.ComputeHash(2, 42, new byte[32], true, new byte[32]);
            var hash4 = MessageLeaf.ComputeHash(1, 42, new byte[32], false, new byte[32]);

            Assert.False(hash1.SequenceEqual(hash2));
            Assert.False(hash1.SequenceEqual(hash3));
            Assert.False(hash1.SequenceEqual(hash4));
        }

        [Fact]
        public void Accumulator_AppendLeaf_IncreasesLeafCount()
        {
            var accumulator = new MessageMerkleAccumulator();

            var leaf = new MessageLeaf
            {
                SourceChainId = 1,
                MessageId = 1,
                AppChainTxHash = new byte[32],
                Success = true,
                DataHash = new byte[32]
            };

            accumulator.AppendLeaf(1, leaf);
            Assert.Equal(1, accumulator.GetLeafCount(1));

            var leaf2 = new MessageLeaf
            {
                SourceChainId = 1,
                MessageId = 2,
                AppChainTxHash = new byte[32],
                Success = true,
                DataHash = new byte[32]
            };

            accumulator.AppendLeaf(1, leaf2);
            Assert.Equal(2, accumulator.GetLeafCount(1));
        }

        [Fact]
        public void Accumulator_MultipleChains_IndependentTrees()
        {
            var accumulator = new MessageMerkleAccumulator();

            var leaf1 = new MessageLeaf { SourceChainId = 1, MessageId = 1, AppChainTxHash = new byte[32], Success = true, DataHash = new byte[32] };
            var leaf2 = new MessageLeaf { SourceChainId = 2, MessageId = 1, AppChainTxHash = new byte[32], Success = true, DataHash = new byte[32] };

            accumulator.AppendLeaf(1, leaf1);
            accumulator.AppendLeaf(2, leaf2);

            Assert.Equal(1, accumulator.GetLeafCount(1));
            Assert.Equal(1, accumulator.GetLeafCount(2));

            var chainIds = accumulator.GetSourceChainIds();
            Assert.Contains((ulong)1, chainIds);
            Assert.Contains((ulong)2, chainIds);
        }

        [Fact]
        public void Accumulator_TracksLastProcessedMessageId()
        {
            var accumulator = new MessageMerkleAccumulator();
            var txHash = new byte[32];
            txHash[0] = 0x01;

            var leaf1 = new MessageLeaf { SourceChainId = 1, MessageId = 1, AppChainTxHash = txHash, Success = true, DataHash = new byte[32] };
            accumulator.AppendLeaf(1, leaf1);
            Assert.Equal((ulong)1, accumulator.GetLastProcessedMessageId(1));

            var leaf2 = new MessageLeaf { SourceChainId = 1, MessageId = 2, AppChainTxHash = txHash, Success = true, DataHash = new byte[32] };
            accumulator.AppendLeaf(1, leaf2);
            Assert.Equal((ulong)2, accumulator.GetLastProcessedMessageId(1));
        }

        [Fact]
        public void Accumulator_RejectsNonContiguousMessageId()
        {
            var accumulator = new MessageMerkleAccumulator();
            var txHash = new byte[32];
            txHash[0] = 0x01;

            accumulator.AppendLeaf(1, new MessageLeaf { SourceChainId = 1, MessageId = 1, AppChainTxHash = txHash, Success = true, DataHash = new byte[32] });

            var ex = Assert.Throws<InvalidOperationException>(() =>
                accumulator.AppendLeaf(1, new MessageLeaf { SourceChainId = 1, MessageId = 3, AppChainTxHash = txHash, Success = true, DataHash = new byte[32] }));
            Assert.Contains("Non-contiguous", ex.Message);
        }

        [Fact]
        public void Accumulator_RejectsDuplicateMessageId()
        {
            var accumulator = new MessageMerkleAccumulator();
            var txHash = new byte[32];
            txHash[0] = 0x01;

            accumulator.AppendLeaf(1, new MessageLeaf { SourceChainId = 1, MessageId = 1, AppChainTxHash = txHash, Success = true, DataHash = new byte[32] });

            var ex = Assert.Throws<InvalidOperationException>(() =>
                accumulator.AppendLeaf(1, new MessageLeaf { SourceChainId = 1, MessageId = 1, AppChainTxHash = txHash, Success = true, DataHash = new byte[32] }));
            Assert.Contains("Duplicate", ex.Message);
        }

        [Fact]
        public void Accumulator_RejectsEmptyTxHash()
        {
            var accumulator = new MessageMerkleAccumulator();

            var ex = Assert.Throws<ArgumentException>(() =>
                accumulator.AppendLeaf(1, new MessageLeaf { SourceChainId = 1, MessageId = 1, AppChainTxHash = Array.Empty<byte>(), Success = true, DataHash = new byte[32] }));
            Assert.Contains("AppChainTxHash", ex.Message);
        }

        [Fact]
        public void Accumulator_GetSnapshot_ReturnsAtomicState()
        {
            var accumulator = new MessageMerkleAccumulator();
            var txHash = new byte[32];
            txHash[0] = 0x01;

            accumulator.AppendLeaf(1, new MessageLeaf { SourceChainId = 1, MessageId = 1, AppChainTxHash = txHash, Success = true, DataHash = new byte[32] });
            accumulator.AppendLeaf(1, new MessageLeaf { SourceChainId = 1, MessageId = 2, AppChainTxHash = txHash, Success = true, DataHash = new byte[32] });

            var (root, lastId) = accumulator.GetSnapshot(1);
            Assert.Equal((ulong)2, lastId);
            Assert.True(root.Length > 0);
            Assert.Equal(accumulator.GetRoot(1), root);
        }

        [Fact]
        public void Accumulator_RootChanges_AfterAppend()
        {
            var accumulator = new MessageMerkleAccumulator();

            var leaf = new MessageLeaf { SourceChainId = 1, MessageId = 1, AppChainTxHash = new byte[32], Success = true, DataHash = new byte[32] };
            accumulator.AppendLeaf(1, leaf);
            var root1 = accumulator.GetRoot(1).ToArray();

            var leaf2 = new MessageLeaf { SourceChainId = 1, MessageId = 2, AppChainTxHash = new byte[32], Success = true, DataHash = new byte[32] };
            accumulator.AppendLeaf(1, leaf2);
            var root2 = accumulator.GetRoot(1).ToArray();

            Assert.False(root1.SequenceEqual(root2));
        }

        [Fact]
        public void Accumulator_GenerateProof_VerifiesCorrectly()
        {
            var accumulator = new MessageMerkleAccumulator();
            var hashProvider = new Sha3KeccackHashProvider();

            var txHash1 = Sha3Keccack.Current.CalculateHash(System.Text.Encoding.UTF8.GetBytes("tx1"));
            var dataHash1 = Sha3Keccack.Current.CalculateHash(System.Text.Encoding.UTF8.GetBytes("data1"));
            var leaf1 = new MessageLeaf { SourceChainId = 1, MessageId = 1, AppChainTxHash = txHash1, Success = true, DataHash = dataHash1 };

            var txHash2 = Sha3Keccack.Current.CalculateHash(System.Text.Encoding.UTF8.GetBytes("tx2"));
            var dataHash2 = Sha3Keccack.Current.CalculateHash(System.Text.Encoding.UTF8.GetBytes("data2"));
            var leaf2 = new MessageLeaf { SourceChainId = 1, MessageId = 2, AppChainTxHash = txHash2, Success = true, DataHash = dataHash2 };

            var txHash3 = Sha3Keccack.Current.CalculateHash(System.Text.Encoding.UTF8.GetBytes("tx3"));
            var dataHash3 = Sha3Keccack.Current.CalculateHash(System.Text.Encoding.UTF8.GetBytes("data3"));
            var leaf3 = new MessageLeaf { SourceChainId = 1, MessageId = 3, AppChainTxHash = txHash3, Success = false, DataHash = dataHash3 };

            accumulator.AppendLeaf(1, leaf1);
            accumulator.AppendLeaf(1, leaf2);
            accumulator.AppendLeaf(1, leaf3);

            var root = accumulator.GetRoot(1);

            var tree = new LeanIncrementalMerkleTree<byte[]>(
                hashProvider,
                new ByteArrayToByteArrayConvertor(),
                PairingConcatType.Sorted);

            for (int leafIndex = 0; leafIndex < 3; leafIndex++)
            {
                var proof = accumulator.GenerateProof(1, leafIndex);
                Assert.NotNull(proof);
                Assert.True(proof.ProofNodes.Count > 0);

                var leafForVerify = leafIndex switch
                {
                    0 => leaf1,
                    1 => leaf2,
                    _ => leaf3
                };

                var encodedData = leafForVerify.GetEncodedData();
                var verified = tree.VerifyProof(proof, encodedData, root);
                Assert.True(verified, $"Proof verification failed for leaf {leafIndex}");
            }
        }

        [Fact]
        public void Accumulator_ProofFails_WithWrongRoot()
        {
            var accumulator = new MessageMerkleAccumulator();
            var hashProvider = new Sha3KeccackHashProvider();

            var leaf = new MessageLeaf { SourceChainId = 1, MessageId = 1, AppChainTxHash = new byte[32], Success = true, DataHash = new byte[32] };
            accumulator.AppendLeaf(1, leaf);

            var proof = accumulator.GenerateProof(1, 0);
            var wrongRoot = new byte[32];
            wrongRoot[0] = 0xFF;

            var tree = new LeanIncrementalMerkleTree<byte[]>(
                hashProvider,
                new ByteArrayToByteArrayConvertor(),
                PairingConcatType.Sorted);

            var encodedData = leaf.GetEncodedData();
            var verified = tree.VerifyProof(proof, encodedData, wrongRoot);
            Assert.False(verified);
        }

        [Fact]
        public void Accumulator_ProofFails_WithWrongLeaf()
        {
            var accumulator = new MessageMerkleAccumulator();
            var hashProvider = new Sha3KeccackHashProvider();

            var leaf = new MessageLeaf { SourceChainId = 1, MessageId = 1, AppChainTxHash = new byte[32], Success = true, DataHash = new byte[32] };
            accumulator.AppendLeaf(1, leaf);

            var root = accumulator.GetRoot(1);
            var proof = accumulator.GenerateProof(1, 0);

            var wrongLeaf = new MessageLeaf { SourceChainId = 1, MessageId = 999, AppChainTxHash = new byte[32], Success = true, DataHash = new byte[32] };

            var tree = new LeanIncrementalMerkleTree<byte[]>(
                hashProvider,
                new ByteArrayToByteArrayConvertor(),
                PairingConcatType.Sorted);

            var wrongEncodedData = wrongLeaf.GetEncodedData();
            var verified = tree.VerifyProof(proof, wrongEncodedData, root);
            Assert.False(verified);
        }

        [Fact]
        public void Accumulator_LargeTree_ProofsVerify()
        {
            var accumulator = new MessageMerkleAccumulator();
            var hashProvider = new Sha3KeccackHashProvider();

            for (int i = 0; i < 100; i++)
            {
                var txHash = Sha3Keccack.Current.CalculateHash(
                    System.Text.Encoding.UTF8.GetBytes($"tx-{i}"));
                var dataHash = Sha3Keccack.Current.CalculateHash(
                    System.Text.Encoding.UTF8.GetBytes($"data-{i}"));

                var leaf = new MessageLeaf
                {
                    SourceChainId = 1,
                    MessageId = (ulong)(i + 1),
                    AppChainTxHash = txHash,
                    Success = i % 3 != 0,
                    DataHash = dataHash
                };
                accumulator.AppendLeaf(1, leaf);
            }

            var root = accumulator.GetRoot(1);
            Assert.Equal(100, accumulator.GetLeafCount(1));

            var tree = new LeanIncrementalMerkleTree<byte[]>(
                hashProvider,
                new ByteArrayToByteArrayConvertor(),
                PairingConcatType.Sorted);

            var indicesToTest = new[] { 0, 1, 49, 50, 98, 99 };
            foreach (var idx in indicesToTest)
            {
                var txHash = Sha3Keccack.Current.CalculateHash(
                    System.Text.Encoding.UTF8.GetBytes($"tx-{idx}"));
                var dataHash = Sha3Keccack.Current.CalculateHash(
                    System.Text.Encoding.UTF8.GetBytes($"data-{idx}"));

                var testLeaf = new MessageLeaf
                {
                    SourceChainId = 1,
                    MessageId = (ulong)(idx + 1),
                    AppChainTxHash = txHash,
                    Success = idx % 3 != 0,
                    DataHash = dataHash
                };

                var proof = accumulator.GenerateProof(1, idx);
                var encodedData = testLeaf.GetEncodedData();
                var verified = tree.VerifyProof(proof, encodedData, root);
                Assert.True(verified, $"Proof failed for leaf index {idx}");
            }
        }

        [Fact]
        public void MessageQueue_EnqueueAndDrain_ReturnsInOrder()
        {
            var queue = new MessageQueue();

            queue.Enqueue(new MessageInfo { SourceChainId = 2, MessageId = 3 });
            queue.Enqueue(new MessageInfo { SourceChainId = 1, MessageId = 2 });
            queue.Enqueue(new MessageInfo { SourceChainId = 1, MessageId = 1 });

            Assert.Equal(3, queue.Count);

            var batch = queue.DrainBatch(10);

            Assert.Equal(3, batch.Count);
            Assert.Equal(0, queue.Count);
            Assert.Equal((ulong)1, batch[0].SourceChainId);
            Assert.Equal((ulong)1, batch[0].MessageId);
            Assert.Equal((ulong)1, batch[1].SourceChainId);
            Assert.Equal((ulong)2, batch[1].MessageId);
            Assert.Equal((ulong)2, batch[2].SourceChainId);
        }

        [Fact]
        public void MessageQueue_DrainBatch_RespectsMaxSize()
        {
            var queue = new MessageQueue();

            for (int i = 0; i < 10; i++)
            {
                queue.Enqueue(new MessageInfo { SourceChainId = 1, MessageId = (ulong)(i + 1) });
            }

            var batch = queue.DrainBatch(3);

            Assert.Equal(3, batch.Count);
            Assert.Equal(7, queue.Count);
        }

        [Fact]
        public void MessageQueue_EnqueueRange_Works()
        {
            var queue = new MessageQueue();
            var messages = new[]
            {
                new MessageInfo { SourceChainId = 1, MessageId = 1 },
                new MessageInfo { SourceChainId = 1, MessageId = 2 },
                new MessageInfo { SourceChainId = 1, MessageId = 3 }
            };

            queue.EnqueueRange(messages);
            Assert.Equal(3, queue.Count);
        }

        [Fact]
        public async Task MessageQueue_ConcurrentEnqueueDrain_NoLoss()
        {
            var queue = new MessageQueue();
            int totalMessages = 1000;
            var allDrained = new System.Collections.Concurrent.ConcurrentBag<MessageInfo>();

            var enqueueTask = Task.Run(() =>
            {
                for (int i = 0; i < totalMessages; i++)
                {
                    queue.Enqueue(new MessageInfo { SourceChainId = 1, MessageId = (ulong)(i + 1) });
                }
            });

            var drainTask = Task.Run(async () =>
            {
                int drained = 0;
                while (drained < totalMessages)
                {
                    var batch = queue.DrainBatch(50);
                    foreach (var m in batch) allDrained.Add(m);
                    drained += batch.Count;
                    if (batch.Count == 0) await Task.Delay(1);
                }
            });

            await Task.WhenAll(enqueueTask, drainTask).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(totalMessages, allDrained.Count);
        }

        [Fact]
        public async Task MessageProcessor_ProcessBatch_CreatesResults()
        {
            var accumulator = new MessageMerkleAccumulator();
            var processor = new MessageProcessor(accumulator);

            var messages = new List<MessageInfo>
            {
                new MessageInfo { SourceChainId = 1, MessageId = 1, Target = "0x1111111111111111111111111111111111111111", Data = new byte[] { 0x01 } },
                new MessageInfo { SourceChainId = 1, MessageId = 2, Target = "0x2222222222222222222222222222222222222222", Data = new byte[] { 0x02 } }
            };

            var result = await processor.ProcessBatchAsync(messages);

            Assert.Equal(2, result.ProcessedCount);
            Assert.Equal(0, result.FailedCount);
            Assert.Equal(2, result.Results.Count);
            Assert.True(result.Results[0].Success);
            Assert.True(result.Results[1].Success);
            Assert.Equal(2, accumulator.GetLeafCount(1));
            Assert.Equal((ulong)2, accumulator.GetLastProcessedMessageId(1));
        }

        [Fact]
        public async Task MessageProcessor_WithCustomExecutor_UsesIt()
        {
            var accumulator = new MessageMerkleAccumulator();
            var customTxHash = new byte[32];
            customTxHash[0] = 0xAA;

            var processor = new MessageProcessor(accumulator, executor: msg => Task.FromResult(new MessageExecutionResult
            {
                TxHash = customTxHash,
                Success = msg.MessageId % 2 == 1,
                GasUsed = 50000
            }));

            var messages = new List<MessageInfo>
            {
                new MessageInfo { SourceChainId = 1, MessageId = 1, Data = new byte[] { 0x01 } },
                new MessageInfo { SourceChainId = 1, MessageId = 2, Data = new byte[] { 0x02 } }
            };

            var result = await processor.ProcessBatchAsync(messages);

            Assert.Equal(2, result.ProcessedCount);
            Assert.Equal(1, result.FailedCount);
            Assert.True(result.Results[0].Success);
            Assert.False(result.Results[1].Success);
            Assert.Equal(customTxHash, result.Results[0].AppChainTxHash);
        }

        [Fact]
        public async Task MessageResultStore_StoreAndRetrieve_ByMessageId()
        {
            var store = new InMemoryMessageResultStore();
            var result = new MessageResult
            {
                SourceChainId = 1,
                MessageId = 42,
                LeafIndex = 5,
                TxHash = Sha3Keccack.Current.CalculateHash(System.Text.Encoding.UTF8.GetBytes("tx42")),
                Success = true,
                DataHash = Sha3Keccack.Current.CalculateHash(System.Text.Encoding.UTF8.GetBytes("data42"))
            };

            await store.StoreAsync(result);

            var retrieved = await store.GetByMessageIdAsync(1, 42);
            Assert.NotNull(retrieved);
            Assert.Equal(42ul, retrieved!.MessageId);
            Assert.Equal(5, retrieved.LeafIndex);
            Assert.True(retrieved.Success);
            Assert.Equal(result.TxHash, retrieved.TxHash);
            Assert.Equal(result.DataHash, retrieved.DataHash);
        }

        [Fact]
        public async Task MessageResultStore_GetByMessageId_ReturnsNullForMissing()
        {
            var store = new InMemoryMessageResultStore();
            var retrieved = await store.GetByMessageIdAsync(1, 999);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task MessageResultStore_GetAllOrderedByLeafIndex_ReturnsOrdered()
        {
            var store = new InMemoryMessageResultStore();

            await store.StoreAsync(new MessageResult { SourceChainId = 1, MessageId = 3, LeafIndex = 2, TxHash = new byte[32], DataHash = new byte[32] });
            await store.StoreAsync(new MessageResult { SourceChainId = 1, MessageId = 1, LeafIndex = 0, TxHash = new byte[32], DataHash = new byte[32] });
            await store.StoreAsync(new MessageResult { SourceChainId = 1, MessageId = 2, LeafIndex = 1, TxHash = new byte[32], DataHash = new byte[32] });

            var all = await store.GetAllBySourceChainOrderedByLeafIndexAsync(1);
            Assert.Equal(3, all.Count);
            Assert.Equal(0, all[0].LeafIndex);
            Assert.Equal(1, all[1].LeafIndex);
            Assert.Equal(2, all[2].LeafIndex);
        }

        [Fact]
        public async Task MessageResultStore_MultipleChainsIndependent()
        {
            var store = new InMemoryMessageResultStore();

            await store.StoreAsync(new MessageResult { SourceChainId = 1, MessageId = 1, LeafIndex = 0, TxHash = new byte[32], DataHash = new byte[32] });
            await store.StoreAsync(new MessageResult { SourceChainId = 2, MessageId = 1, LeafIndex = 0, TxHash = new byte[32], DataHash = new byte[32] });

            Assert.Equal(1, await store.GetCountAsync(1));
            Assert.Equal(1, await store.GetCountAsync(2));

            var chainIds = await store.GetSourceChainIdsAsync();
            Assert.Contains(1ul, chainIds);
            Assert.Contains(2ul, chainIds);
        }

        [Fact]
        public async Task Accumulator_RebuildFromStore_ReproducesTree()
        {
            var store = new InMemoryMessageResultStore();
            var accumulator1 = new MessageMerkleAccumulator();
            var hashProvider = new Sha3KeccackHashProvider();

            for (int i = 0; i < 5; i++)
            {
                var txHash = Sha3Keccack.Current.CalculateHash(System.Text.Encoding.UTF8.GetBytes($"tx-{i}"));
                var dataHash = Sha3Keccack.Current.CalculateHash(System.Text.Encoding.UTF8.GetBytes($"data-{i}"));
                var leaf = new MessageLeaf
                {
                    SourceChainId = 1,
                    MessageId = (ulong)(i + 1),
                    AppChainTxHash = txHash,
                    Success = true,
                    DataHash = dataHash
                };
                int leafIndex = accumulator1.AppendLeaf(1, leaf);

                await store.StoreAsync(new MessageResult
                {
                    SourceChainId = 1,
                    MessageId = (ulong)(i + 1),
                    LeafIndex = leafIndex,
                    TxHash = txHash,
                    Success = true,
                    DataHash = dataHash
                });
            }

            var originalRoot = accumulator1.GetRoot(1).ToArray();

            var accumulator2 = new MessageMerkleAccumulator();
            var rebuilt = await accumulator2.RebuildFromStoreAsync(store);
            Assert.Equal(5, rebuilt);

            var rebuiltRoot = accumulator2.GetRoot(1).ToArray();
            Assert.Equal(originalRoot, rebuiltRoot);

            var tree = new LeanIncrementalMerkleTree<byte[]>(
                hashProvider,
                new ByteArrayToByteArrayConvertor(),
                PairingConcatType.Sorted);

            for (int i = 0; i < 5; i++)
            {
                var proof1 = accumulator1.GenerateProof(1, i);
                var proof2 = accumulator2.GenerateProof(1, i);

                Assert.Equal(proof1.ProofNodes.Count, proof2.ProofNodes.Count);
                for (int j = 0; j < proof1.ProofNodes.Count; j++)
                {
                    Assert.Equal(proof1.ProofNodes[j], proof2.ProofNodes[j]);
                }
            }
        }

        [Fact]
        public async Task MessageProcessor_WithStore_StoresResults()
        {
            var accumulator = new MessageMerkleAccumulator();
            var store = new InMemoryMessageResultStore();
            var fixedTxHash = new byte[32];
            fixedTxHash[0] = 0xCC;

            var processor = new MessageProcessor(accumulator, store, msg => Task.FromResult(new MessageExecutionResult
            {
                TxHash = fixedTxHash,
                Success = true,
                GasUsed = 21000
            }));

            var messages = new List<MessageInfo>
            {
                new MessageInfo { SourceChainId = 1, MessageId = 1, Data = new byte[] { 0x01 } },
                new MessageInfo { SourceChainId = 1, MessageId = 2, Data = new byte[] { 0x02 } },
                new MessageInfo { SourceChainId = 1, MessageId = 3, Data = new byte[] { 0x03 } }
            };

            await processor.ProcessBatchAsync(messages);

            Assert.Equal(3, await store.GetCountAsync(1));

            var result1 = await store.GetByMessageIdAsync(1, 1);
            Assert.NotNull(result1);
            Assert.Equal(0, result1!.LeafIndex);
            Assert.True(result1.Success);
            Assert.Equal(fixedTxHash, result1.TxHash);

            var result3 = await store.GetByMessageIdAsync(1, 3);
            Assert.NotNull(result3);
            Assert.Equal(2, result3!.LeafIndex);

            var root = accumulator.GetRoot(1);
            var hashProvider = new Sha3KeccackHashProvider();
            var tree = new LeanIncrementalMerkleTree<byte[]>(
                hashProvider,
                new ByteArrayToByteArrayConvertor(),
                PairingConcatType.Sorted);

            var proof = accumulator.GenerateProof(1, result1.LeafIndex);
            var verified = tree.VerifyProof(proof, result1.ToLeaf().GetEncodedData(), root);
            Assert.True(verified, "Proof from store-tracked result must verify");
        }

        [Fact]
        public async Task MessageProcessor_ProofVerifies_AfterProcessing()
        {
            var accumulator = new MessageMerkleAccumulator();
            var hashProvider = new Sha3KeccackHashProvider();

            var fixedTxHash = new byte[32];
            fixedTxHash[0] = 0xBB;

            var processor = new MessageProcessor(accumulator, executor: msg => Task.FromResult(new MessageExecutionResult
            {
                TxHash = fixedTxHash,
                Success = true,
                GasUsed = 21000
            }));

            var messages = new List<MessageInfo>
            {
                new MessageInfo { SourceChainId = 1, MessageId = 1, Data = new byte[] { 0xDE, 0xAD } },
                new MessageInfo { SourceChainId = 1, MessageId = 2, Data = new byte[] { 0xBE, 0xEF } },
                new MessageInfo { SourceChainId = 1, MessageId = 3, Data = new byte[] { 0xCA, 0xFE } }
            };

            await processor.ProcessBatchAsync(messages);

            var root = accumulator.GetRoot(1);

            var tree = new LeanIncrementalMerkleTree<byte[]>(
                hashProvider,
                new ByteArrayToByteArrayConvertor(),
                PairingConcatType.Sorted);

            for (int i = 0; i < 3; i++)
            {
                var dataHash = Sha3Keccack.Current.CalculateHash(messages[i].Data);
                var encodedData = MessageLeaf.EncodeLeafData(1, (ulong)(i + 1), fixedTxHash, true, dataHash);

                var proof = accumulator.GenerateProof(1, i);
                var verified = tree.VerifyProof(proof, encodedData, root);
                Assert.True(verified, $"Proof verification failed for message {i + 1}");
            }
        }
    }
}
