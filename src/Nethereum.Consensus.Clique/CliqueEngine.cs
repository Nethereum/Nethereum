using System.Numerics;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Consensus;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;

namespace Nethereum.Consensus.Clique
{
    public class CliqueEngine : IConsensusEngine, IDisposable
    {
        private readonly CliqueConfig _config;
        private readonly ILogger<CliqueEngine>? _logger;
        private readonly EthECKey? _signerKey;
        private readonly string _signerAddress;
        private readonly Dictionary<long, CliqueSnapshot> _snapshots = new();
        private readonly object _snapshotLock = new();
        private CliqueSnapshot _currentSnapshot;

        public const int DIFF_IN_TURN = 2;
        public const int DIFF_OUT_OF_TURN = 1;
        public const int EXTRA_VANITY = 32;
        public const int EXTRA_SEAL = 65;

        public string Name => "clique";
        public CliqueConfig Config => _config;
        public string SignerAddress => _signerAddress;
        public CliqueSnapshot CurrentSnapshot => _currentSnapshot;

        public event EventHandler<BlockProducedEventArgs>? BlockProduced;

        public CliqueEngine(CliqueConfig config, ILogger<CliqueEngine>? logger = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger;

            if (!string.IsNullOrEmpty(config.LocalSignerPrivateKey))
            {
                _signerKey = new EthECKey(config.LocalSignerPrivateKey);
                _signerAddress = _signerKey.GetPublicAddress().ToLowerInvariant();
            }
            else
            {
                _signerAddress = config.LocalSignerAddress.ToLowerInvariant();
            }

            _currentSnapshot = new CliqueSnapshot
            {
                BlockNumber = 0,
                Signers = config.InitialSigners.Select(s => s.ToLowerInvariant()).ToList()
            };
        }

        public void ApplyGenesisSigners(IEnumerable<string> signers)
        {
            lock (_snapshotLock)
            {
                _currentSnapshot = new CliqueSnapshot
                {
                    BlockNumber = 0,
                    Signers = signers.Select(s => s.ToLowerInvariant()).ToList()
                };
                _snapshots[0] = _currentSnapshot.Clone();
            }
        }

        public bool IsInTurn(long blockNumber, string signerAddress)
        {
            lock (_snapshotLock)
            {
                if (_currentSnapshot.TotalSigners == 0)
                    return false;

                var signerIndex = _currentSnapshot.SignerIndex(signerAddress.ToLowerInvariant());
                if (signerIndex < 0)
                    return false;

                var turn = (int)(blockNumber % _currentSnapshot.TotalSigners);
                return turn == signerIndex;
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

                return true;
            }
        }

        public async Task<TimeSpan> GetSigningDelayAsync(long blockNumber, CancellationToken cancellationToken = default)
        {
            var inTurn = IsInTurn(blockNumber, _signerAddress);

            if (inTurn)
            {
                return TimeSpan.Zero;
            }
            else
            {
                var random = new Random();
                var wiggle = random.Next(0, _config.WiggleTimeMs);
                return TimeSpan.FromMilliseconds(_config.WiggleTimeMs + wiggle);
            }
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
                if (!_currentSnapshot.IsAuthorized(signer))
                    return CliqueValidationResult.Fail($"Unauthorized signer: {signer}");

                var expectedDifficulty = GetDifficulty((long)header.BlockNumber, signer);
                if ((BigInteger)header.Difficulty != expectedDifficulty)
                    return CliqueValidationResult.Fail($"Wrong difficulty: expected {expectedDifficulty}");
            }

            return CliqueValidationResult.Success(signer);
        }

        public void ApplyBlock(BlockHeader header, string signer, byte[]? blockHash = null)
        {
            lock (_snapshotLock)
            {
                var newSnapshot = _currentSnapshot.Clone();
                newSnapshot.BlockNumber = (long)header.BlockNumber;
                newSnapshot.BlockHash = blockHash ?? BlockHeaderEncoder.Current.EncodeCliqueSigHeaderAndHash(header);

                if ((long)header.BlockNumber % _config.EpochLength == 0)
                {
                    newSnapshot.Votes.Clear();
                    newSnapshot.VoteTally.Clear();

                    if (header.ExtraData != null && header.ExtraData.Length > EXTRA_VANITY + EXTRA_SEAL)
                    {
                        var signersLength = header.ExtraData.Length - EXTRA_VANITY - EXTRA_SEAL;
                        if (signersLength % 20 == 0)
                        {
                            newSnapshot.Signers.Clear();
                            var numSigners = signersLength / 20;
                            for (int i = 0; i < numSigners; i++)
                            {
                                var signerBytes = new byte[20];
                                Array.Copy(header.ExtraData, EXTRA_VANITY + (i * 20), signerBytes, 0, 20);
                                newSnapshot.Signers.Add("0x" + BitConverter.ToString(signerBytes).Replace("-", "").ToLowerInvariant());
                            }
                        }
                    }
                }
                else if (_config.EnableVoting && header.Nonce != null && header.Nonce.Length == 8)
                {
                    var nonceValue = BitConverter.ToUInt64(header.Nonce, 0);
                    if (nonceValue == 0xFFFFFFFFFFFFFFFF || nonceValue == 0)
                    {
                        var authorize = nonceValue == 0xFFFFFFFFFFFFFFFF;
                        var coinbase = header.Coinbase ?? "";

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

                _currentSnapshot = newSnapshot;
                _snapshots[(long)header.BlockNumber] = newSnapshot.Clone();

                while (_snapshots.Count > 100)
                {
                    var oldest = _snapshots.Keys.Min();
                    _snapshots.Remove(oldest);
                }
            }
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
                        snapshot.Signers.Sort();
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
