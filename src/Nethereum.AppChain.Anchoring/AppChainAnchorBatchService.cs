using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Anchoring.AppChainAnchor;
using Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition;
using Nethereum.CoreChain.DataAvailability;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Web3;

namespace Nethereum.AppChain.Anchoring
{
    public class AppChainAnchorBatchService : IAnchorService
    {
        private readonly AppChainAnchorService _contractService;
        private readonly ILogger<AppChainAnchorBatchService>? _logger;
        private readonly ulong _appChainId;
        private readonly byte[] _genesisHash;
        private readonly object _stateLock = new();

        private byte[] _lastEndBlockHash = new byte[32];
        private ulong _lastEndBlock;
        private bool _initialized;

        public AppChainAnchorBatchService(
            AnchorConfig config,
            IWeb3 web3,
            ulong appChainId,
            byte[] genesisHash,
            ILogger<AppChainAnchorBatchService>? logger = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _appChainId = appChainId;
            _genesisHash = genesisHash ?? new byte[32];
            _logger = logger;
            _contractService = new AppChainAnchorService(web3, config.AnchorContractAddress);
        }

        public async Task InitializeAsync()
        {
            if (_initialized) return;

            try
            {
                var result = await _contractService.GetLatestAnchorQueryAsync(_appChainId).ConfigureAwait(false);
                if (result.EndBlock > 0)
                {
                    lock (_stateLock)
                    {
                        _lastEndBlock = result.EndBlock;
                        _lastEndBlockHash = result.EndBlockHash ?? new byte[32];
                    }

                    _logger?.LogInformation(
                        "Recovered anchor state: lastBlock={Block}, hash={Hash}",
                        _lastEndBlock, _lastEndBlockHash.ToHex(true).Substring(0, 18));
                }
                else
                {
                    _logger?.LogInformation("No previous anchors on chain, starting fresh");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not recover anchor state, starting fresh");
            }

            _initialized = true;
        }

        public Task<AnchorInfo> AnchorBlockAsync(
            BigInteger blockNumber, byte[] stateRoot, byte[] transactionsRoot, byte[] receiptsRoot)
        {
            return AnchorBlockAsync(blockNumber, stateRoot, transactionsRoot, receiptsRoot, null, null);
        }

        public async Task<AnchorInfo> AnchorBlockAsync(
            BigInteger blockNumber,
            byte[] stateRoot,
            byte[] transactionsRoot,
            byte[] receiptsRoot,
            byte[] blockHash,
            AnchorSubmissionPayload submission)
        {
            var anchorInfo = new AnchorInfo
            {
                BlockNumber = blockNumber,
                StateRoot = stateRoot,
                TransactionsRoot = transactionsRoot,
                ReceiptsRoot = receiptsRoot,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            try
            {
                var endBlock = (ulong)blockNumber;
                ulong startBlock;
                byte[] previousAnchorHash;

                lock (_stateLock)
                {
                    startBlock = _lastEndBlock > 0 ? _lastEndBlock + 1 : 1UL;
                    previousAnchorHash = _lastEndBlockHash;
                }

                var endBlockHash = blockHash ?? new byte[32];
                if (blockHash == null)
                    _logger?.LogWarning("Block {Block}: hash unavailable, using zeros", blockNumber);

                var blockHashesRoot = BlockHashesTree.ComputeRoot(new List<byte[]> { endBlockHash });
                var proofSystem = (byte)(submission?.OnChainProofSystem ?? AnchoringOnChainProofSystem.NoProof);
                var calldataPayload = submission?.ProofBytes ?? Array.Empty<byte>();

                var anchor = new AggregatedAnchor
                {
                    ChainId = _appChainId,
                    GenesisHash = _genesisHash,
                    StartBlock = startBlock,
                    EndBlock = endBlock,
                    AnchorVersion = 1,
                    ProofSystem = proofSystem,
                    EndBlockHash = endBlockHash,
                    PreviousAnchorHash = previousAnchorHash,
                    BlockHashesRoot = blockHashesRoot,
                    PostStateRoot = stateRoot ?? new byte[32],
                    ManifestHash = new byte[32]
                };

                _logger?.LogInformation(
                    "Submitting anchor: blocks {Start}-{End}, proofSystem={PS}, calldata={Size}b",
                    startBlock, endBlock, proofSystem, calldataPayload.Length);

                var receipt = await _contractService.SubmitAnchorRequestAndWaitForReceiptAsync(
                    anchor, calldataPayload).ConfigureAwait(false);

                if (receipt?.Status?.Value == 1)
                {
                    lock (_stateLock)
                    {
                        _lastEndBlock = endBlock;
                        _lastEndBlockHash = endBlockHash;
                    }

                    anchorInfo.AnchorTxHash = receipt.TransactionHash?.HexToByteArray();
                    anchorInfo.AnchorBlockNumber = receipt.BlockNumber?.Value;
                    anchorInfo.GasUsed = receipt.GasUsed != null ? (long)receipt.GasUsed.Value : 0;
                    anchorInfo.Status = AnchorStatus.Confirmed;

                    _logger?.LogInformation("Anchored blocks {Start}-{End} in tx {TxHash}",
                        startBlock, endBlock, receipt.TransactionHash);
                }
                else
                {
                    anchorInfo.Status = AnchorStatus.Failed;
                    anchorInfo.ErrorMessage = "Transaction reverted";
                    _logger?.LogError("Anchor tx failed for blocks {Start}-{End}", startBlock, endBlock);
                }
            }
            catch (Exception ex)
            {
                anchorInfo.Status = AnchorStatus.Failed;
                anchorInfo.ErrorMessage = ex.Message;
                _logger?.LogError(ex, "Failed to anchor block {BlockNumber}", blockNumber);
            }

            return anchorInfo;
        }

        public async Task<AnchorInfo?> GetAnchorAsync(BigInteger blockNumber)
        {
            try
            {
                var result = await _contractService.GetLatestAnchorQueryAsync(_appChainId).ConfigureAwait(false);
                if (result.EndBlock == 0) return null;

                return new AnchorInfo
                {
                    BlockNumber = result.EndBlock,
                    StateRoot = result.PostStateRoot,
                    Status = AnchorStatus.Confirmed
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get anchor for block {BlockNumber}", blockNumber);
                return null;
            }
        }

        public async Task<BigInteger> GetLatestAnchoredBlockAsync()
        {
            try
            {
                var result = await _contractService.GetLatestAnchorQueryAsync(_appChainId).ConfigureAwait(false);
                return result.EndBlock;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get latest anchored block");
                return BigInteger.Zero;
            }
        }

        public async Task<bool> VerifyAnchorAsync(
            BigInteger blockNumber, byte[] stateRoot, byte[] transactionsRoot, byte[] receiptsRoot)
        {
            try
            {
                return await _contractService.VerifyStateRootQueryAsync(_appChainId, stateRoot).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to verify anchor for block {BlockNumber}", blockNumber);
                return false;
            }
        }
    }
}
