using System.Numerics;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Consensus;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.Consensus.Clique
{
    public class CliqueEngine : IConsensusEngine, IDisposable
    {
        private readonly CliqueConfig _config;
        private readonly ILogger<CliqueEngine>? _logger;
        private readonly EthECKey? _signerKey;
        private readonly string _signerAddress;
        private readonly Dictionary<string, CliqueSnapshot> _snapshots = new();
        private readonly object _snapshotLock = new();
        private readonly ICliqueSnapshotStore? _snapshotStore;
        private CliqueSnapshot _genesisSnapshot;
        private CliqueSnapshot _currentSnapshot;

        public const int SnapshotInterval = 1024;
        public const int DIFF_IN_TURN = 2;
        public const int DIFF_OUT_OF_TURN = 1;
        public const int EXTRA_VANITY = 32;
        public const int EXTRA_SEAL = 65;

        public static readonly byte[] NONCE_AUTH = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        public static readonly byte[] NONCE_DROP = new byte[8];

        public string Name => "clique";
        public CliqueConfig Config => _config;
        public string SignerAddress => _signerAddress;
        public CliqueSnapshot CurrentSnapshot => _currentSnapshot;

        public event EventHandler<BlockProducedEventArgs>? BlockProduced;

        public CliqueEngine(CliqueConfig config, ILogger<CliqueEngine>? logger = null)
            : this(config, logger, null)
        {
        }

        public CliqueEngine(CliqueConfig config, ILogger<CliqueEngine>? logger, ICliqueSnapshotStore? snapshotStore)
        {
            _snapshotStore = snapshotStore;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger;

            if (!string.IsNullOrEmpty(config.LocalSignerPrivateKey))
            {
                _signerKey = new EthECKey(config.LocalSignerPrivateKey);
                _signerAddress = _signerKey.GetPublicAddress().ToLowerInvariant();
            }
            else
            {
                _signerAddress = config.LocalSignerAddress?.ToLowerInvariant() ?? string.Empty;
            }

            _currentSnapshot = new CliqueSnapshot
            {
                BlockNumber = 0,
                Signers = CliqueSnapshot.NormalizedAndSorted(config.InitialSigners)
            };
            _genesisSnapshot = _currentSnapshot;
        }

        public void ApplyGenesisSigners(IEnumerable<string> signers)
        {
            lock (_snapshotLock)
            {
                _currentSnapshot = new CliqueSnapshot
                {
                    BlockNumber = 0,
                    Signers = CliqueSnapshot.NormalizedAndSorted(signers)
                };
                _genesisSnapshot = _currentSnapshot;
                _snapshots.Clear();
            }
        }

        [NethereumDocExample(DocSection.Consensus, "clique", "Ask whether a snapshot exists for a block hash")]
        public bool HasSnapshot(byte[] blockHash)
        {
            lock (_snapshotLock)
            {
                if (_snapshots.ContainsKey(blockHash.ToHex()))
                    return true;

                var persisted = _snapshotStore?.Load(blockHash);
                if (persisted == null || !ByteUtil.AreEqual(persisted.BlockHash, blockHash))
                    return false;

                Store(persisted);
                return true;
            }
        }

        private CliqueSnapshot? SnapshotOfParent(BlockHeader header)
        {
            if ((long)header.BlockNumber == 0)
                return null;

            if (header.ParentHash != null && _snapshots.TryGetValue(header.ParentHash.ToHex(), out var known))
                return known;

            return (long)header.BlockNumber == 1 ? _genesisSnapshot : null;
        }

        private static bool IsInTurn(CliqueSnapshot snapshot, long blockNumber, string signerAddress)
        {
            if (snapshot.TotalSigners == 0)
                return false;

            var signerIndex = snapshot.SignerIndex(signerAddress.ToLowerInvariant());
            if (signerIndex < 0)
                return false;

            return (int)(blockNumber % snapshot.TotalSigners) == signerIndex;
        }

        public bool IsInTurn(long blockNumber, string signerAddress)
        {
            lock (_snapshotLock)
            {
                return IsInTurn(_currentSnapshot, blockNumber, signerAddress);
            }
        }

        public BigInteger GetDifficulty(long blockNumber, string signerAddress)
        {
            return IsInTurn(blockNumber, signerAddress) ? DIFF_IN_TURN : DIFF_OUT_OF_TURN;
        }

        public bool IsAuthorizedSigner(string address)
        {
            lock (_snapshotLock)
            {
                return _currentSnapshot.IsAuthorized(address);
            }
        }

        public bool CanProduceBlock(long blockNumber)
        {
            if (_signerKey == null)
            {
                _logger?.LogWarning("CanProduceBlock: No signer key configured");
                return false;
            }

            lock (_snapshotLock)
            {
                if (!_currentSnapshot.IsAuthorized(_signerAddress))
                {
                    _logger?.LogWarning("CanProduceBlock: Signer {Address} not authorized. Authorized signers: [{Signers}]",
                        _signerAddress, string.Join(", ", _currentSnapshot.Signers));
                    return false;
                }

                var limit = _config.CalculateRecentSignersLimit(_currentSnapshot.TotalSigners);
                if (_currentSnapshot.SignedWithinLimit(_signerAddress, blockNumber, limit))
                {
                    _logger?.LogDebug("CanProduceBlock: Signer {Address} signed recently, must wait for others before block {Number}",
                        _signerAddress, blockNumber);
                    return false;
                }

                return true;
            }
        }

        public async Task<TimeSpan> GetSigningDelayAsync(long blockNumber, CancellationToken cancellationToken = default)
        {
            var untilPeriodElapsed = TimeUntilEarliestNextBlock();

            if (IsInTurn(blockNumber, _signerAddress))
            {
                return untilPeriodElapsed;
            }
            else
            {
                var random = new Random();
                var wiggle = random.Next(0, _config.WiggleTimeMs);
                return untilPeriodElapsed + TimeSpan.FromMilliseconds(_config.WiggleTimeMs + wiggle);
            }
        }

        [NethereumDocExample(DocSection.Consensus, "clique", "The earliest timestamp a produced block may carry")]
        public long NextBlockTimestamp(BlockHeader? parent)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return parent == null ? now : Math.Max((long)parent.Timestamp + _config.BlockPeriodSeconds, now);
        }

        private TimeSpan TimeUntilEarliestNextBlock()
        {
            long headTimestamp;
            lock (_snapshotLock)
            {
                headTimestamp = _currentSnapshot.Timestamp;
            }

            if (headTimestamp == 0)
                return TimeSpan.Zero;

            var earliest = DateTimeOffset.FromUnixTimeSeconds(headTimestamp + _config.BlockPeriodSeconds);
            var wait = earliest - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        [NethereumDocExample(DocSection.Consensus, "clique", "Recover the signer that authored a block")]
        public string AuthorOf(BlockHeader header)
        {
            if (header.ExtraData == null || header.ExtraData.Length < EXTRA_VANITY + EXTRA_SEAL)
                return _signerAddress;

            var sealStart = header.ExtraData.Length - EXTRA_SEAL;
            var sealed_ = false;
            for (var i = sealStart; i < header.ExtraData.Length; i++)
                if (header.ExtraData[i] != 0) { sealed_ = true; break; }

            return sealed_ ? RecoverSigner(header) ?? header.Coinbase : _signerAddress;
        }

        private static bool IsParentOf(BlockHeader parent, BlockHeader header)
        {
            if ((long)parent.BlockNumber + 1 != (long)header.BlockNumber)
                return false;

            return header.ParentHash != null
                && ByteUtil.AreEqual(BlockHashCalculator.ForHeader(parent), header.ParentHash);
        }

        public byte[] SignBlock(BlockHeader header)
        {
            if (_signerKey == null)
                throw new InvalidOperationException("No signer key configured");

            var sealHash = GetSealHash(header);
            var signature = _signerKey.SignAndCalculateV(sealHash);

            return signature.CreateStringSignature().HexToByteArray();
        }

        public byte[] GetSealHash(BlockHeader header)
        {
            return BlockHeaderEncoder.Current.EncodeCliqueSigHeaderAndHash(header);
        }

        public string? RecoverSigner(BlockHeader header)
        {
            if (header.ExtraData == null || header.ExtraData.Length < EXTRA_VANITY + EXTRA_SEAL)
                return null;

            var signature = new byte[65];
            Array.Copy(header.ExtraData, header.ExtraData.Length - EXTRA_SEAL, signature, 0, EXTRA_SEAL);

            var sealHash = GetSealHash(header);
            var ecdsaSignature = ECDSASignatureFactory.ExtractECDSASignature(signature);
            var recoveredKey = EthECKey.RecoverFromSignature(new EthECDSASignature(ecdsaSignature), sealHash);

            return recoveredKey.GetPublicAddress().ToLowerInvariant();
        }

        public bool ValidateBlock(BlockHeader header, BlockHeader? parent)
        {
            var result = ValidateBlockInternal(header, parent);
            return result.IsValid;
        }

        public CliqueValidationResult ValidateBlockInternal(BlockHeader header, BlockHeader? parent)
        {
            if (header.ExtraData == null || header.ExtraData.Length < EXTRA_VANITY + EXTRA_SEAL)
                return CliqueValidationResult.Fail("Invalid extraData length");

            if (IsCheckpointBlock((long)header.BlockNumber))
            {
                var checkpointSigners = DecodeCheckpointSigners(header.ExtraData);
                if (checkpointSigners != null && !IsAscendingByteOrder(checkpointSigners))
                    return CliqueValidationResult.Fail("Checkpoint signer list is not sorted in ascending byte order");
            }

            if (header.MixHash != null && header.MixHash.Any(b => b != 0))
                return CliqueValidationResult.Fail("MixHash must be zero");

            if (header.Nonce != null && header.Nonce.Length == 8)
            {
                var nonceValue = BitConverter.ToUInt64(header.Nonce, 0);
                if (nonceValue != 0 && nonceValue != 0xFFFFFFFFFFFFFFFF)
                    return CliqueValidationResult.Fail("Invalid Clique nonce");
            }

            if ((BigInteger)header.Difficulty != DIFF_IN_TURN && (BigInteger)header.Difficulty != DIFF_OUT_OF_TURN)
                return CliqueValidationResult.Fail("Invalid difficulty");

            if (parent != null && !IsParentOf(parent, header))
                return CliqueValidationResult.Fail(
                    $"Unknown ancestor: the supplied parent is not the parent of block {header.BlockNumber}");

            if (parent != null && (long)parent.Timestamp + _config.BlockPeriodSeconds > (long)header.Timestamp)
                return CliqueValidationResult.Fail(
                    $"Invalid timestamp: block {header.BlockNumber} at {header.Timestamp} is earlier than parent {parent.Timestamp} plus period {_config.BlockPeriodSeconds}");

            string? signer;
            try
            {
                signer = RecoverSigner(header);
            }
            catch (Exception ex)
            {
                return CliqueValidationResult.Fail($"Failed to recover signer: {ex.Message}");
            }

            if (signer == null)
                return CliqueValidationResult.Fail("Could not recover signer");

            lock (_snapshotLock)
            {
                var snapshot = SnapshotOfParent(header);
                if (snapshot == null)
                    return CliqueValidationResult.Fail($"Unknown ancestor: no snapshot for the parent of block {header.BlockNumber}");

                if (!snapshot.IsAuthorized(signer))
                    return CliqueValidationResult.Fail($"Unauthorized signer: {signer}");

                var recentSignersLimit = _config.CalculateRecentSignersLimit(snapshot.TotalSigners);
                if (snapshot.SignedWithinLimit(signer, (long)header.BlockNumber, recentSignersLimit))
                    return CliqueValidationResult.Fail(
                        $"Signer {signer} signed recently: only one block out of every {recentSignersLimit} may be signed by the same signer");

                if (IsCheckpointBlock((long)header.BlockNumber)
                    && !CheckpointMatches(snapshot, DecodeCheckpointSigners(header.ExtraData)))
                    return CliqueValidationResult.Fail("Checkpoint signer list does not match the snapshot signers");

                var expectedDifficulty = IsInTurn(snapshot, (long)header.BlockNumber, signer) ? DIFF_IN_TURN : DIFF_OUT_OF_TURN;
                if ((BigInteger)header.Difficulty != expectedDifficulty)
                    return CliqueValidationResult.Fail($"Wrong difficulty: expected {expectedDifficulty}");
            }

            return CliqueValidationResult.Success(signer);
        }

        public void ApplyBlock(BlockHeader header, string signer, byte[]? blockHash = null)
        {
            lock (_snapshotLock)
            {
                _currentSnapshot = RecordSnapshot(header, signer, blockHash);
            }
        }

        internal void RecordBlock(BlockHeader header, string signer, byte[] blockHash)
        {
            lock (_snapshotLock)
            {
                RecordSnapshot(header, signer, blockHash);
            }
        }

        internal void RecordCheckpoint(BlockHeader header, byte[] blockHash)
        {
            var signers = DecodeCheckpointSigners(header.ExtraData);
            if (signers == null)
                return;

            lock (_snapshotLock)
            {
                var snapshot = new CliqueSnapshot
                {
                    BlockNumber = (long)header.BlockNumber,
                    Timestamp = (long)header.Timestamp,
                    BlockHash = blockHash,
                    Signers = CliqueSnapshot.NormalizedAndSorted(signers)
                };
                Store(snapshot);
            }
        }

        internal void AdoptGenesisHead()
        {
            lock (_snapshotLock)
            {
                _currentSnapshot = _genesisSnapshot.Clone();
            }
        }

        internal void AdoptHead(byte[] blockHash)
        {
            lock (_snapshotLock)
            {
                if (_snapshots.TryGetValue(blockHash.ToHex(), out var snapshot))
                    _currentSnapshot = snapshot.Clone();
            }
        }

        private void Store(CliqueSnapshot snapshot)
        {
            _snapshots[snapshot.BlockHash.ToHex()] = snapshot.Clone();

            while (_snapshots.Count > 100)
            {
                var oldest = _snapshots.OrderBy(kv => kv.Value.BlockNumber).First().Key;
                _snapshots.Remove(oldest);
            }
        }

        private CliqueSnapshot RecordSnapshot(BlockHeader header, string signer, byte[]? blockHash)
        {
            var newSnapshot = (SnapshotOfParent(header) ?? _currentSnapshot).Clone();
            newSnapshot.BlockNumber = (long)header.BlockNumber;
            newSnapshot.Timestamp = (long)header.Timestamp;
            newSnapshot.BlockHash = blockHash ?? new Sha3Keccack().CalculateHash(BlockHeaderEncoder.Current.Encode(header));

            if ((long)header.BlockNumber % _config.EpochLength == 0)
            {
                newSnapshot.Votes.Clear();
                newSnapshot.VoteTally.Clear();
            }
            else if (_config.EnableVoting && header.Nonce != null && header.Nonce.Length == 8)
            {
                var nonceValue = BitConverter.ToUInt64(header.Nonce, 0);
                if (nonceValue == 0xFFFFFFFFFFFFFFFF || nonceValue == 0)
                {
                    var authorize = nonceValue == 0xFFFFFFFFFFFFFFFF;
                    var coinbase = string.IsNullOrEmpty(header.Coinbase)
                        ? ""
                        : AddressUtil.Current.ConvertToValid20ByteAddressLowerCase(header.Coinbase);

                    if (!string.IsNullOrEmpty(coinbase) && coinbase != "0x0000000000000000000000000000000000000000")
                    {
                        var voteKey = $"{signer}:{coinbase}";
                        newSnapshot.Votes[voteKey] = new CliqueVote
                        {
                            Signer = signer,
                            Target = coinbase.ToLowerInvariant(),
                            Authorize = authorize,
                            BlockNumber = (long)header.BlockNumber
                        };

                        UpdateVoteTally(newSnapshot);
                    }
                }
            }

            var recentSignersLimit = _config.CalculateRecentSignersLimit(newSnapshot.TotalSigners);
            newSnapshot.RecordSigner((long)header.BlockNumber, signer, recentSignersLimit);

            Store(newSnapshot);
            if (newSnapshot.BlockNumber % SnapshotInterval == 0)
                _snapshotStore?.Save(newSnapshot);
            return newSnapshot;
        }

        private void UpdateVoteTally(CliqueSnapshot snapshot)
        {
            snapshot.VoteTally.Clear();

            foreach (var vote in snapshot.Votes.Values)
            {
                var key = $"{vote.Target}:{vote.Authorize}";
                if (!snapshot.VoteTally.ContainsKey(key))
                    snapshot.VoteTally[key] = 0;
                snapshot.VoteTally[key]++;

                if (snapshot.VoteTally[key] >= snapshot.RequiredVotes)
                {
                    if (vote.Authorize && !snapshot.Signers.Contains(vote.Target))
                    {
                        snapshot.Signers.Add(vote.Target);
                        snapshot.Signers = CliqueSnapshot.NormalizedAndSorted(snapshot.Signers);
                    }
                    else if (!vote.Authorize && snapshot.Signers.Contains(vote.Target))
                    {
                        snapshot.Signers.Remove(vote.Target);
                    }

                    var keysToRemove = snapshot.Votes
                        .Where(v => v.Value.Target == vote.Target)
                        .Select(v => v.Key)
                        .ToList();

                    foreach (var k in keysToRemove)
                        snapshot.Votes.Remove(k);

                    snapshot.VoteTally.Remove($"{vote.Target}:true");
                    snapshot.VoteTally.Remove($"{vote.Target}:false");
                }
            }
        }

        private bool IsCheckpointBlock(long blockNumber) => blockNumber % _config.EpochLength == 0;

        private List<string>? DecodeCheckpointSigners(byte[]? extraData)
        {
            if (extraData == null)
                return null;

            var signersLength = extraData.Length - EXTRA_VANITY - EXTRA_SEAL;
            if (signersLength <= 0 || signersLength % 20 != 0)
                return null;

            var numSigners = signersLength / 20;
            var signers = new List<string>(numSigners);
            for (int i = 0; i < numSigners; i++)
            {
                var signerBytes = new byte[20];
                Array.Copy(extraData, EXTRA_VANITY + (i * 20), signerBytes, 0, 20);
                signers.Add("0x" + BitConverter.ToString(signerBytes).Replace("-", "").ToLowerInvariant());
            }
            return signers;
        }

        private static bool CheckpointMatches(CliqueSnapshot snapshot, List<string>? listed)
        {
            var listedSigners = listed ?? new List<string>();
            return listedSigners.SequenceEqual(snapshot.Signers, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsAscendingByteOrder(List<string> signers)
        {
            for (int i = 1; i < signers.Count; i++)
                if (string.CompareOrdinal(signers[i - 1], signers[i]) >= 0)
                    return false;
            return true;
        }

        public byte[] PrepareExtraData(long blockNumber, object? vote = null)
        {
            var vanity = new byte[EXTRA_VANITY];
            byte[] signersData = Array.Empty<byte>();

            if (blockNumber % _config.EpochLength == 0)
            {
                lock (_snapshotLock)
                {
                    signersData = new byte[_currentSnapshot.TotalSigners * 20];
                    for (int i = 0; i < _currentSnapshot.Signers.Count; i++)
                    {
                        var signerBytes = _currentSnapshot.Signers[i].HexToByteArray();
                        Array.Copy(signerBytes, 0, signersData, i * 20, 20);
                    }
                }
            }

            var seal = new byte[EXTRA_SEAL];
            var extra = new byte[EXTRA_VANITY + signersData.Length + EXTRA_SEAL];
            Array.Copy(vanity, 0, extra, 0, EXTRA_VANITY);
            if (signersData.Length > 0)
                Array.Copy(signersData, 0, extra, EXTRA_VANITY, signersData.Length);
            Array.Copy(seal, 0, extra, EXTRA_VANITY + signersData.Length, EXTRA_SEAL);

            return extra;
        }

        public void InsertSignature(byte[] extraData, byte[] signature)
        {
            if (signature.Length != EXTRA_SEAL)
                throw new ArgumentException($"Signature must be {EXTRA_SEAL} bytes");

            Array.Copy(signature, 0, extraData, extraData.Length - EXTRA_SEAL, EXTRA_SEAL);
        }

        public void Dispose()
        {
        }
    }

    public class CliqueValidationResult
    {
        public bool IsValid { get; }
        public string? Error { get; }
        public string? Signer { get; }

        private CliqueValidationResult(bool isValid, string? error, string? signer)
        {
            IsValid = isValid;
            Error = error;
            Signer = signer;
        }

        public static CliqueValidationResult Success(string signer) => new(true, null, signer);
        public static CliqueValidationResult Fail(string error) => new(false, error, null);
    }

    public class BlockProducedEventArgs : EventArgs
    {
        public BlockHeader Header { get; }
        public byte[] BlockHash { get; }
        public string Signer { get; }

        public BlockProducedEventArgs(BlockHeader header, byte[] blockHash, string signer)
        {
            Header = header;
            BlockHash = blockHash;
            Signer = signer;
        }
    }
}
