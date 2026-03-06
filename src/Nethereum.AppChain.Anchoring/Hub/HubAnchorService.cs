using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Accounts;

namespace Nethereum.AppChain.Anchoring.Hub
{
    public class HubAnchorService : IAnchorService
    {
        private readonly HubConfig _config;
        private readonly AppChainHubService? _hubService;
        private readonly ILogger<HubAnchorService>? _logger;

        private long _lastProcessedMessageId;

        public HubAnchorService(HubConfig config, ILogger<HubAnchorService>? logger = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger;

            if (!string.IsNullOrEmpty(_config.HubRpcUrl) &&
                !string.IsNullOrEmpty(_config.SequencerPrivateKey) &&
                !string.IsNullOrEmpty(_config.HubContractAddress))
            {
                var account = new Account(_config.SequencerPrivateKey, _config.HubChainId);
                var web3 = new Web3.Web3(account, _config.HubRpcUrl);
                _hubService = new AppChainHubService(web3, _config.HubContractAddress);
            }
        }

        public ulong LastProcessedMessageId => (ulong)Interlocked.Read(ref _lastProcessedMessageId);

        public void SetProcessedUpToMessageId(ulong messageId)
        {
            Interlocked.Exchange(ref _lastProcessedMessageId, (long)messageId);
        }

        public async Task<AnchorInfo> AnchorBlockAsync(
            BigInteger blockNumber,
            byte[] stateRoot,
            byte[] transactionsRoot,
            byte[] receiptsRoot)
        {
            var anchorInfo = new AnchorInfo
            {
                BlockNumber = blockNumber,
                StateRoot = stateRoot,
                TransactionsRoot = transactionsRoot,
                ReceiptsRoot = receiptsRoot,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            if (_hubService == null)
            {
                _logger?.LogWarning("Hub anchor service not configured, skipping anchor for block {BlockNumber}", blockNumber);
                anchorInfo.Status = AnchorStatus.Failed;
                anchorInfo.ErrorMessage = "Hub anchor service not configured";
                return anchorInfo;
            }

            try
            {
                _logger?.LogInformation("Anchoring block {BlockNumber} to hub contract {Contract} (chainId={ChainId})",
                    blockNumber, _config.HubContractAddress, _config.ChainId);

                var receipt = await _hubService.AnchorRequestAndWaitForReceiptAsync(
                    _config.ChainId,
                    (ulong)blockNumber,
                    stateRoot,
                    transactionsRoot,
                    receiptsRoot,
                    LastProcessedMessageId,
                    Array.Empty<byte>());

                if (receipt.Succeeded())
                {
                    anchorInfo.AnchorTxHash = receipt.TransactionHash.HexToByteArray();
                    anchorInfo.AnchorBlockNumber = receipt.BlockNumber.Value;
                    anchorInfo.Status = AnchorStatus.Confirmed;

                    _logger?.LogInformation("Block {BlockNumber} anchored to hub in tx {TxHash} (processedUpTo={MessageId})",
                        blockNumber, receipt.TransactionHash, LastProcessedMessageId);
                }
                else
                {
                    anchorInfo.Status = AnchorStatus.Failed;
                    anchorInfo.ErrorMessage = "Transaction failed";
                    _logger?.LogError("Failed to anchor block {BlockNumber} to hub: transaction failed", blockNumber);
                }
            }
            catch (Exception ex)
            {
                anchorInfo.Status = AnchorStatus.Failed;
                anchorInfo.ErrorMessage = ex.Message;
                _logger?.LogError(ex, "Failed to anchor block {BlockNumber} to hub", blockNumber);
            }

            return anchorInfo;
        }

        public async Task<AnchorInfo?> GetAnchorAsync(BigInteger blockNumber)
        {
            if (_hubService == null) return null;

            try
            {
                var result = await _hubService.GetAnchorQueryAsync(_config.ChainId, (ulong)blockNumber);
                if (result.Timestamp == 0) return null;

                return new AnchorInfo
                {
                    BlockNumber = blockNumber,
                    StateRoot = result.StateRoot,
                    TransactionsRoot = result.TxRoot,
                    ReceiptsRoot = result.ReceiptRoot,
                    Timestamp = (long)result.Timestamp,
                    Status = AnchorStatus.Confirmed
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get hub anchor for block {BlockNumber}", blockNumber);
                return null;
            }
        }

        public async Task<BigInteger> GetLatestAnchoredBlockAsync()
        {
            if (_hubService == null) return BigInteger.Zero;

            try
            {
                var info = await _hubService.GetAppChainInfoQueryAsync(_config.ChainId);
                return info.LatestBlock;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get latest anchored block from hub");
                return BigInteger.Zero;
            }
        }

        public async Task<bool> VerifyAnchorAsync(
            BigInteger blockNumber,
            byte[] stateRoot,
            byte[] transactionsRoot,
            byte[] receiptsRoot)
        {
            if (_hubService == null) return false;

            try
            {
                return await _hubService.VerifyAnchorQueryAsync(
                    _config.ChainId,
                    (ulong)blockNumber,
                    stateRoot,
                    transactionsRoot,
                    receiptsRoot);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to verify hub anchor for block {BlockNumber}", blockNumber);
                return false;
            }
        }

        public async Task<bool> VerifyAnchorProofAsync(ulong blockNumber, byte[] proof)
        {
            if (_hubService == null) return false;

            try
            {
                return await _hubService.VerifyAnchorProofQueryAsync(_config.ChainId, blockNumber, proof);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to verify hub anchor proof for block {BlockNumber}", blockNumber);
                return false;
            }
        }

        public async Task<HubInfo?> GetAppChainInfoAsync()
        {
            if (_hubService == null) return null;

            try
            {
                var result = await _hubService.GetAppChainInfoQueryAsync(_config.ChainId);
                return new HubInfo
                {
                    ChainId = _config.ChainId,
                    Owner = result.Owner,
                    Sequencer = result.Sequencer,
                    LatestBlock = result.LatestBlock,
                    LastProcessedMessageId = result.LastProcessedMessageId,
                    NextMessageId = result.NextMessageId,
                    Registered = result.Registered
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get hub AppChain info");
                return null;
            }
        }
    }
}
