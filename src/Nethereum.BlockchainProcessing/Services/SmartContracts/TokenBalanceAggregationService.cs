using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#else
using Nethereum.JsonRpc.Client;
#endif
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.ProgressRepositories;

namespace Nethereum.BlockchainProcessing.Services.SmartContracts
{
    public class TokenBalanceAggregationService
    {
        private readonly ITokenTransferLogRepository _transferLogRepository;
        private readonly ITokenBalanceRepository _balanceRepository;
        private readonly INFTInventoryRepository _nftRepository;
        private readonly IBlockProgressRepository _progressRepository;
        private readonly ILogger _logger;

        public TokenBalanceAggregationService(
            ITokenTransferLogRepository transferLogRepository,
            ITokenBalanceRepository balanceRepository,
            INFTInventoryRepository nftRepository,
            IBlockProgressRepository progressRepository,
            ILogger logger = null)
        {
            _transferLogRepository = transferLogRepository;
            _balanceRepository = balanceRepository;
            _nftRepository = nftRepository;
            _progressRepository = progressRepository;
            _logger = logger;
        }

        public async Task RollbackAggregationAsync(BigInteger fromBlock, BigInteger toBlock)
        {
            var nonCanonicalTransferLogRepo = _transferLogRepository as INonCanonicalTokenTransferLogRepository;

            for (var block = fromBlock; block <= toBlock; block++)
            {
                if (nonCanonicalTransferLogRepo != null)
                {
                    await nonCanonicalTransferLogRepo.MarkNonCanonicalAsync(block).ConfigureAwait(false);
                }

                await _balanceRepository.DeleteByBlockNumberAsync(block).ConfigureAwait(false);
                await _nftRepository.DeleteByBlockNumberAsync(block).ConfigureAwait(false);
            }

#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
            _logger?.LogInformation(
                "Rolled back token aggregation for blocks {FromBlock} to {ToBlock}.",
                fromBlock, toBlock);
#else
            _logger?.LogInformation(
                $"Rolled back token aggregation for blocks {fromBlock} to {toBlock}.");
#endif
        }

        public async Task ProcessFromCheckpointAsync(CancellationToken cancellationToken = default)
        {
            var lastProcessed = await _progressRepository.GetLastBlockNumberProcessedAsync().ConfigureAwait(false);
            var fromBlock = lastProcessed.HasValue ? lastProcessed.Value + 1 : 0;

            var consecutiveEmpty = 0;
            const int maxConsecutiveEmpty = 100;

            while (!cancellationToken.IsCancellationRequested)
            {
                var logs = await _transferLogRepository.GetByBlockNumberAsync((long)fromBlock).ConfigureAwait(false);
                var logList = logs?.ToList() ?? new List<ITokenTransferLogView>();

                if (logList.Count == 0)
                {
                    consecutiveEmpty++;
                    if (consecutiveEmpty > maxConsecutiveEmpty)
                        break;
                    fromBlock++;
                    continue;
                }

                consecutiveEmpty = 0;

                foreach (var log in logList)
                {
                    if (!log.IsCanonical) continue;
                    await ProcessTransferAsync(log).ConfigureAwait(false);
                }

                await _progressRepository.UpsertProgressAsync(fromBlock).ConfigureAwait(false);
                fromBlock++;
            }
        }

        public async Task ProcessTransferLogsAsync(IEnumerable<ITokenTransferLogView> logs)
        {
            foreach (var log in logs)
            {
                if (!log.IsCanonical) continue;
                await ProcessTransferAsync(log).ConfigureAwait(false);
            }
        }

        public async Task ProcessTransferAsync(ITokenTransferLogView transfer)
        {
            switch (transfer.TokenType)
            {
                case "ERC20":
                    await ProcessERC20TransferAsync(transfer).ConfigureAwait(false);
                    break;
                case "ERC721":
                    await ProcessERC721TransferAsync(transfer).ConfigureAwait(false);
                    break;
                case "ERC1155":
                    await ProcessERC1155TransferAsync(transfer).ConfigureAwait(false);
                    break;
            }
        }

        private async Task ProcessERC20TransferAsync(ITokenTransferLogView transfer)
        {
            if (!BigInteger.TryParse(transfer.Amount ?? "0", out var amount)) return;

            if (!IsZeroAddress(transfer.FromAddress))
            {
                await UpdateBalanceAsync(transfer.FromAddress, transfer.ContractAddress,
                    -amount, "ERC20", transfer.BlockNumber).ConfigureAwait(false);
            }

            if (!IsZeroAddress(transfer.ToAddress))
            {
                await UpdateBalanceAsync(transfer.ToAddress, transfer.ContractAddress,
                    amount, "ERC20", transfer.BlockNumber).ConfigureAwait(false);
            }
        }

        private async Task ProcessERC721TransferAsync(ITokenTransferLogView transfer)
        {
            if (!IsZeroAddress(transfer.FromAddress))
            {
                var existingNft = await _nftRepository.GetByTokenAsync(
                    transfer.ContractAddress, transfer.TokenId).ConfigureAwait(false);

                if (existingNft != null)
                {
                    await _nftRepository.UpsertAsync(new NFTInventory
                    {
                        Address = existingNft.Address,
                        ContractAddress = existingNft.ContractAddress,
                        TokenId = existingNft.TokenId,
                        Amount = "0",
                        TokenType = "ERC721",
                        LastUpdatedBlockNumber = transfer.BlockNumber
                    }).ConfigureAwait(false);

                    await UpdateBalanceAsync(transfer.FromAddress, transfer.ContractAddress,
                        -1, "ERC721", transfer.BlockNumber).ConfigureAwait(false);
                }
            }

            if (!IsZeroAddress(transfer.ToAddress))
            {
                await _nftRepository.UpsertAsync(new NFTInventory
                {
                    Address = transfer.ToAddress,
                    ContractAddress = transfer.ContractAddress,
                    TokenId = transfer.TokenId,
                    Amount = "1",
                    TokenType = "ERC721",
                    LastUpdatedBlockNumber = transfer.BlockNumber
                }).ConfigureAwait(false);

                await UpdateBalanceAsync(transfer.ToAddress, transfer.ContractAddress,
                    1, "ERC721", transfer.BlockNumber).ConfigureAwait(false);
            }
        }

        private async Task ProcessERC1155TransferAsync(ITokenTransferLogView transfer)
        {
            if (!BigInteger.TryParse(transfer.Amount ?? "0", out var amount)) return;

            if (!IsZeroAddress(transfer.FromAddress))
            {
                var existingNft = await _nftRepository.GetByTokenAsync(
                    transfer.ContractAddress, transfer.TokenId).ConfigureAwait(false);

                if (existingNft != null)
                {
                    var existingAmount = BigInteger.TryParse(existingNft.Amount ?? "0", out var ea) ? ea : BigInteger.Zero;
                    var newAmount = existingAmount - amount;

                    if (newAmount <= 0)
                    {
                        await _nftRepository.UpsertAsync(new NFTInventory
                        {
                            Address = transfer.FromAddress,
                            ContractAddress = transfer.ContractAddress,
                            TokenId = transfer.TokenId,
                            Amount = "0",
                            TokenType = "ERC1155",
                            LastUpdatedBlockNumber = transfer.BlockNumber
                        }).ConfigureAwait(false);
                    }
                    else
                    {
                        await _nftRepository.UpsertAsync(new NFTInventory
                        {
                            Address = transfer.FromAddress,
                            ContractAddress = transfer.ContractAddress,
                            TokenId = transfer.TokenId,
                            Amount = newAmount.ToString(),
                            TokenType = "ERC1155",
                            LastUpdatedBlockNumber = transfer.BlockNumber
                        }).ConfigureAwait(false);
                    }
                }
            }

            if (!IsZeroAddress(transfer.ToAddress))
            {
                var existingNft = await _nftRepository.GetByTokenAsync(
                    transfer.ContractAddress, transfer.TokenId).ConfigureAwait(false);

                var existingAmount = BigInteger.Zero;
                if (existingNft != null && existingNft.Address?.ToLowerInvariant() == transfer.ToAddress?.ToLowerInvariant())
                {
                    BigInteger.TryParse(existingNft.Amount ?? "0", out existingAmount);
                }

                await _nftRepository.UpsertAsync(new NFTInventory
                {
                    Address = transfer.ToAddress,
                    ContractAddress = transfer.ContractAddress,
                    TokenId = transfer.TokenId,
                    Amount = (existingAmount + amount).ToString(),
                    TokenType = "ERC1155",
                    LastUpdatedBlockNumber = transfer.BlockNumber
                }).ConfigureAwait(false);
            }
        }

        private async Task UpdateBalanceAsync(string address, string contractAddress,
            BigInteger delta, string tokenType, long blockNumber)
        {
            var balances = await _balanceRepository.GetByAddressAsync(address).ConfigureAwait(false);
            var existing = balances?.FirstOrDefault(b =>
                b.ContractAddress?.ToLowerInvariant() == contractAddress?.ToLowerInvariant());

            var currentBalance = BigInteger.Zero;
            if (existing != null)
            {
                BigInteger.TryParse(existing.Balance ?? "0", out currentBalance);
            }

            var newBalance = currentBalance + delta;
            if (newBalance < 0) newBalance = BigInteger.Zero;

            await _balanceRepository.UpsertAsync(new TokenBalance
            {
                Address = address,
                ContractAddress = contractAddress,
                Balance = newBalance.ToString(),
                TokenType = tokenType,
                LastUpdatedBlockNumber = blockNumber
            }).ConfigureAwait(false);
        }

        private static bool IsZeroAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return true;
            return address == "0x0000000000000000000000000000000000000000" ||
                   address == "0x0" ||
                   address.TrimStart('0').Length == 0;
        }
    }
}
