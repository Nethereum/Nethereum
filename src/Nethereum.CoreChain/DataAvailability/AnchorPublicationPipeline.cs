using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Proving;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.DataAvailability
{
    public class AnchorPublicationResult
    {
        public AnchorScope Scope { get; init; }
        public byte[] EncodedPayload { get; init; }
        public ProofPublication ProofPublication { get; init; }
        public DaPublication DaPublication { get; init; }
        public long? PreviousValidatedBlock { get; init; }
    }

    public class AnchorPublicationPipeline
    {
        private readonly StateModel _stateModel;
        private readonly IWitnessStore _witnessStore;
        private readonly IProofRequestQueue _proofQueue;
        private readonly AnchorReceiptRecorder _receiptRecorder;

        private readonly List<IAnchorPayloadContributor> _contributors = new();
        private IProofPublisher _proofPublisher;
        private IDataAvailabilityPublisher _daPublisher;

        private long _lastValidatedBlock;
        private readonly object _validatedLock = new object();

        public AnchorPublicationPipeline(
            StateModel stateModel,
            IWitnessStore witnessStore,
            IProofRequestQueue proofQueue = null,
            AnchorReceiptRecorder receiptRecorder = null,
            long lastValidatedBlock = 0)
        {
            _stateModel = stateModel;
            _witnessStore = witnessStore;
            _proofQueue = proofQueue;
            _receiptRecorder = receiptRecorder ?? new AnchorReceiptRecorder();
            _lastValidatedBlock = lastValidatedBlock;
        }

        public AnchorPublicationPipeline AddContributor(IAnchorPayloadContributor contributor)
        {
            _contributors.Add(contributor);
            return this;
        }

        public AnchorPublicationPipeline WithProofPublisher(IProofPublisher publisher)
        {
            _proofPublisher = publisher;
            return this;
        }

        public AnchorPublicationPipeline WithDaPublisher(IDataAvailabilityPublisher publisher)
        {
            _daPublisher = publisher;
            return this;
        }

        public AnchorReceiptRecorder ReceiptRecorder => _receiptRecorder;

        public async Task<AnchorPublicationResult> ExecuteAsync(
            AnchorScope scope, CancellationToken ct = default)
        {
            if (scope.StateRoot == null)
                throw new ArgumentNullException(nameof(scope), "AnchorScope.StateRoot is required");

            var sections = new List<AnchorPayloadSection>();
            ProofPublication proofPub = null;
            DaPublication daPub = null;
            long? previousValidated = null;

            sections.Add(new AnchorPayloadSection
            {
                Type = AnchorPayloadSectionType.StateRoot,
                Bytes = scope.StateRoot
            });

            if (scope.TransactionsRoot != null)
                sections.Add(new AnchorPayloadSection
                {
                    Type = AnchorPayloadSectionType.TxRoot,
                    Bytes = scope.TransactionsRoot
                });

            if (scope.ReceiptsRoot != null)
                sections.Add(new AnchorPayloadSection
                {
                    Type = AnchorPayloadSectionType.ReceiptRoot,
                    Bytes = scope.ReceiptsRoot
                });

            // Step 2: Publish external/blob proof if publisher configured
            if (_proofPublisher != null)
            {
                var proofResult = await TryGetProofForAdvancement(scope, ct);
                if (proofResult != null)
                {
                    if (proofResult.ProverComputedStateRoot != null &&
                        scope.StateRoot != null &&
                        !Nethereum.Util.ByteUtil.AreEqual(proofResult.ProverComputedStateRoot, scope.StateRoot))
                    {
                        proofResult = null;
                    }
                }
                if (proofResult != null)
                {
                    var bundle = new ProofBundle
                    {
                        ProofBytes = proofResult.ProofBytes,
                        ElfHash = proofResult.ElfHash,
                        ProverMode = proofResult.ProverMode,
                        BlockNumber = proofResult.BlockNumber,
                        StateRootVerified = proofResult.StateRootVerified,
                        BlockHashVerified = proofResult.BlockHashVerified,
                        ProverComputedStateRoot = proofResult.ProverComputedStateRoot,
                        ProverComputedBlockHash = proofResult.ProverComputedBlockHash
                    };
                    proofPub = await _proofPublisher.PublishAsync(bundle, scope, ct);

                    if (proofPub?.CommitmentHash != null)
                        sections.Add(new AnchorPayloadSection
                        {
                            Type = AnchorPayloadSectionType.ProofCommitment,
                            Bytes = proofPub.CommitmentHash
                        });
                }
            }

            // Step 2b: Publish external/blob DA if publisher configured
            if (_daPublisher != null)
            {
                var witness = await _witnessStore.GetWitnessAsync(scope.EndBlock);
                if (witness != null && witness.Length > 0)
                {
                    var payload = new DaPayload
                    {
                        Data = witness,
                        Key = new ContentKey
                        {
                            ChainId = scope.ChainId,
                            StartBlock = scope.StartBlock,
                            EndBlock = scope.EndBlock
                        },
                        Kind = scope.Kind == AnchorKind.Batch ? DaPayloadKind.Batch : DaPayloadKind.Block
                    };
                    daPub = await _daPublisher.PublishAsync(payload, scope, ct);

                    if (daPub?.Commitment?.CommitmentHash != null)
                        sections.Add(new AnchorPayloadSection
                        {
                            Type = AnchorPayloadSectionType.DaCommitment,
                            Bytes = daPub.Commitment.CommitmentHash
                        });
                }
            }

            // Step 3: Collect inline contributors
            foreach (var contributor in _contributors)
            {
                var section = await contributor.ContributeAsync(scope, ct);
                if (section != null)
                    sections.Add(section);
            }

            // Step 8: Advance proof pointer from previously proven scopes
            previousValidated = await TryAdvanceValidatedPointer(scope, ct);
            if (previousValidated.HasValue)
            {
                sections.Add(new AnchorPayloadSection
                {
                    Type = AnchorPayloadSectionType.PreviousValidatedPointer,
                    Bytes = WriteLong(previousValidated.Value)
                });
            }

            // Step 4: Build versioned payload
            var anchorPayload = AnchorPayloadCodec.Build(_stateModel, scope.Kind, sections);
            var encoded = AnchorPayloadCodec.Encode(anchorPayload);

            // Step 7: Enqueue current scope for proving
            if (_proofQueue != null)
                await _proofQueue.EnqueueAsync(scope.EndBlock);

            return new AnchorPublicationResult
            {
                Scope = scope,
                EncodedPayload = encoded,
                ProofPublication = proofPub,
                DaPublication = daPub,
                PreviousValidatedBlock = previousValidated
            };
        }

        public void RecordAnchorTx(long blockNumber, byte[] txHash, byte[] encodedPayload)
        {
            _receiptRecorder.Record(blockNumber, txHash, encodedPayload);
        }

        private async Task<BlockProofResult> TryGetProofForAdvancement(AnchorScope scope, CancellationToken ct)
        {
            if (_witnessStore == null) return null;

            var unproven = await _witnessStore.GetUnprovenBlockNumbersAsync();
            if (unproven == null || unproven.Count == 0) return null;

            foreach (var bn in unproven)
            {
                if ((long)bn >= scope.StartBlock) continue;
                var proof = await _witnessStore.GetProofAsync(bn);
                if (proof?.ProofBytes == null) continue;
                if (proof.ProverComputedStateRoot != null && !proof.StateRootVerified) continue;
                if (proof.ProverComputedBlockHash != null && !proof.BlockHashVerified) continue;
                return proof;
            }

            return null;
        }

        private async Task<long?> TryAdvanceValidatedPointer(AnchorScope scope, CancellationToken ct)
        {
            if (_witnessStore == null) return null;

            long startFrom;
            lock (_validatedLock) { startFrom = _lastValidatedBlock; }

            long newest = startFrom;
            for (long b = startFrom + 1; b < scope.StartBlock; b++)
            {
                ct.ThrowIfCancellationRequested();
                var proof = await _witnessStore.GetProofAsync(b);
                if (proof?.ProofBytes == null) break;
                newest = b;
            }

            if (newest > startFrom)
            {
                lock (_validatedLock) { _lastValidatedBlock = newest; }
                return newest;
            }

            return null;
        }

        private static byte[] WriteLong(long value)
        {
            var bytes = new byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
            return bytes;
        }
    }
}
