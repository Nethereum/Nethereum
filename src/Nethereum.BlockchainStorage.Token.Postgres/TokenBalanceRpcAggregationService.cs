using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainStorage.Token.Postgres.Repositories;
using Nethereum.Contracts;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;
using ERC20Def = Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using ERC721Def = Nethereum.Contracts.Standards.ERC721.ContractDefinition;
using ERC1155Def = Nethereum.Contracts.Standards.ERC1155.ContractDefinition;

namespace Nethereum.BlockchainStorage.Token.Postgres
{
    public class TokenBalanceRpcAggregationService
    {
        private const string TokenTypeERC20 = "ERC20";
        private const string TokenTypeERC721 = "ERC721";
        private const string TokenTypeERC1155 = "ERC1155";
        private const string TokenTypeERC721Owner = "ERC721_OWNER";

        private readonly TokenPostgresDbContext _context;
        private readonly BalanceAggregationProgressRepository _progressRepository;
        private readonly ITokenBalanceRepository _balanceRepository;
        private readonly INFTInventoryRepository _nftRepository;
        private readonly TokenBalanceAggregationOptions _options;
        private readonly ILogger<TokenBalanceRpcAggregationService> _logger;

        public TokenBalanceRpcAggregationService(
            TokenPostgresDbContext context,
            BalanceAggregationProgressRepository progressRepository,
            ITokenBalanceRepository balanceRepository,
            INFTInventoryRepository nftRepository,
            IOptions<TokenBalanceAggregationOptions> options,
            ILogger<TokenBalanceRpcAggregationService> logger = null)
        {
            _context = context;
            _progressRepository = progressRepository;
            _balanceRepository = balanceRepository;
            _nftRepository = nftRepository;
            _options = options.Value;
            _logger = logger;
        }

        public async Task ProcessFromCheckpointAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.RpcUrl))
            {
                _logger?.LogWarning("TokenBalanceAggregation:RpcUrl is not configured. Skipping.");
                return;
            }

            var lastRowIndex = await _progressRepository.GetLastProcessedRowIndexAsync().ConfigureAwait(false);
            var web3 = new Nethereum.Web3.Web3(_options.RpcUrl);
            var client = web3.Client;

            await RecoverReorgedAccountsAsync(client, lastRowIndex, cancellationToken).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                var batch = await _context.TokenTransferLogs
                    .Where(l => l.RowIndex > lastRowIndex && l.IsCanonical)
                    .OrderBy(l => l.RowIndex)
                    .Take(_options.BatchSize)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (batch.Count == 0)
                    break;

                foreach (var blockGroup in batch.GroupBy(l => l.BlockNumber))
                {
                    var blockNumber = blockGroup.Key;
                    var blockParam = new BlockParameter(new HexBigInteger(new BigInteger(blockNumber)));

                    await ProcessBlockBalancesAsync(client, blockGroup.ToList(), blockNumber, blockParam).ConfigureAwait(false);
                }

                lastRowIndex = batch[batch.Count - 1].RowIndex;
                await _progressRepository.UpsertProgressAsync(lastRowIndex).ConfigureAwait(false);

                _context.ChangeTracker.Clear();

                _logger?.LogInformation(
                    "Aggregated token balances for {Count} transfer logs up to RowIndex {RowIndex}",
                    batch.Count, lastRowIndex);
            }
        }

        private async Task ProcessBlockBalancesAsync(
            IClient client,
            List<TokenTransferLog> logs,
            long blockNumber,
            BlockParameter blockParam)
        {
            var calls = new List<IMulticallInputOutput>();
            var callMeta = new List<(string Address, string Contract, string TokenType, string TokenId)>();

            BuildBalanceCalls(logs, calls, callMeta);

            if (calls.Count == 0)
                return;

            var results = await ExecuteBatchAsync(client, blockParam, calls).ConfigureAwait(false);

            var balancesToUpsert = new List<TokenBalance>();
            var nftToUpsert = new List<NFTInventory>();

            for (var i = 0; i < results.Count; i++)
            {
                var (call, hasError) = results[i];
                var meta = callMeta[i];

                if (hasError)
                {
                    _logger?.LogWarning(
                        "Batch RPC call failed for {Type} {Address} on {Contract} at block {Block}",
                        meta.TokenType, meta.Address, meta.Contract, blockNumber);
                    continue;
                }

                CollectCallResult(call, meta, blockNumber, balancesToUpsert, nftToUpsert);
            }

            if (balancesToUpsert.Count > 0)
                await _balanceRepository.UpsertBatchAsync(balancesToUpsert).ConfigureAwait(false);

            if (nftToUpsert.Count > 0)
                await _nftRepository.UpsertBatchAsync(nftToUpsert).ConfigureAwait(false);
        }

        private async Task RecoverReorgedAccountsAsync(
            IClient client,
            long lastRowIndex,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var nonCanonicalLogs = await _context.TokenTransferLogs
                    .Where(l => !l.IsCanonical && l.RowIndex <= lastRowIndex)
                    .OrderBy(l => l.RowIndex)
                    .Take(_options.BatchSize)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (nonCanonicalLogs.Count == 0)
                    break;

                var ethBlockNumber = new EthBlockNumber(client);
                var latestBlock = await ethBlockNumber.SendRequestAsync().ConfigureAwait(false);
                var blockParam = new BlockParameter(latestBlock);
                var blockNumberLong = (long)latestBlock.Value;

                _logger?.LogInformation(
                    "Reorg recovery: found {Count} non-canonical transfer logs, re-querying affected accounts at block {Block}",
                    nonCanonicalLogs.Count, blockNumberLong);

                var calls = new List<IMulticallInputOutput>();
                var callMeta = new List<(string Address, string Contract, string TokenType, string TokenId)>();

                BuildBalanceCalls(nonCanonicalLogs, calls, callMeta);

                if (calls.Count > 0)
                {
                    var results = await ExecuteBatchAsync(client, blockParam, calls).ConfigureAwait(false);

                    var balancesToUpsert = new List<TokenBalance>();
                    var nftToUpsert = new List<NFTInventory>();

                    for (var i = 0; i < results.Count; i++)
                    {
                        var (call, hasError) = results[i];
                        var meta = callMeta[i];

                        if (hasError)
                        {
                            _logger?.LogWarning(
                                "Reorg recovery batch call failed for {Type} {Address} on {Contract}",
                                meta.TokenType, meta.Address, meta.Contract);
                            continue;
                        }

                        CollectCallResult(call, meta, blockNumberLong, balancesToUpsert, nftToUpsert);
                    }

                    if (balancesToUpsert.Count > 0)
                        await _balanceRepository.UpsertBatchAsync(balancesToUpsert).ConfigureAwait(false);

                    if (nftToUpsert.Count > 0)
                        await _nftRepository.UpsertBatchAsync(nftToUpsert).ConfigureAwait(false);
                }

                _context.TokenTransferLogs.RemoveRange(nonCanonicalLogs);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _context.ChangeTracker.Clear();

                _logger?.LogInformation(
                    "Reorg recovery batch: processed {Calls} balance queries, removed {Removed} non-canonical logs.",
                    calls.Count, nonCanonicalLogs.Count);
            }
        }

        private void BuildBalanceCalls(
            List<TokenTransferLog> logs,
            List<IMulticallInputOutput> calls,
            List<(string Address, string Contract, string TokenType, string TokenId)> callMeta)
        {
            foreach (var (address, contract) in ExtractAccounts(logs, TokenTypeERC20))
            {
                var fn = new ERC20Def.BalanceOfFunction { Owner = address };
                calls.Add(new MulticallInputOutput<ERC20Def.BalanceOfFunction, ERC20Def.BalanceOfOutputDTO>(fn, contract));
                callMeta.Add((address, contract, TokenTypeERC20, null));
            }

            foreach (var (address, contract) in ExtractAccounts(logs, TokenTypeERC721))
            {
                var fn = new ERC721Def.BalanceOfFunction { Owner = address };
                calls.Add(new MulticallInputOutput<ERC721Def.BalanceOfFunction, ERC721Def.BalanceOfOutputDTO>(fn, contract));
                callMeta.Add((address, contract, TokenTypeERC721, null));
            }

            var erc721Tokens = logs
                .Where(l => l.TokenType == TokenTypeERC721 && !string.IsNullOrEmpty(l.TokenId))
                .Select(l => (Contract: l.ContractAddress?.ToLowerInvariant(), TokenId: l.TokenId))
                .Where(x => x.Contract != null)
                .Distinct()
                .ToList();

            foreach (var (contract, tokenId) in erc721Tokens)
            {
                var fn = new ERC721Def.OwnerOfFunction { TokenId = BigInteger.Parse(tokenId) };
                calls.Add(new MulticallInputOutput<ERC721Def.OwnerOfFunction, ERC721Def.OwnerOfOutputDTO>(fn, contract));
                callMeta.Add((null, contract, TokenTypeERC721Owner, tokenId));
            }

            foreach (var (address, contract, tokenId) in ExtractErc1155Items(logs))
            {
                var fn = new ERC1155Def.BalanceOfFunction { Account = address, Id = BigInteger.Parse(tokenId) };
                calls.Add(new MulticallInputOutput<ERC1155Def.BalanceOfFunction, ERC1155Def.BalanceOfOutputDTO>(fn, contract));
                callMeta.Add((address, contract, TokenTypeERC1155, tokenId));
            }
        }

        private void CollectCallResult(
            IMulticallInputOutput call,
            (string Address, string Contract, string TokenType, string TokenId) meta,
            long blockNumber,
            List<TokenBalance> balances,
            List<NFTInventory> nftItems)
        {
            try
            {
                switch (meta.TokenType)
                {
                    case TokenTypeERC20:
                    {
                        var output = ((MulticallInputOutput<ERC20Def.BalanceOfFunction, ERC20Def.BalanceOfOutputDTO>)call).Output;
                        balances.Add(new TokenBalance
                        {
                            Address = meta.Address,
                            ContractAddress = meta.Contract,
                            Balance = output.Balance.ToString(),
                            TokenType = TokenTypeERC20,
                            LastUpdatedBlockNumber = blockNumber
                        });
                        break;
                    }
                    case TokenTypeERC721:
                    {
                        var output = ((MulticallInputOutput<ERC721Def.BalanceOfFunction, ERC721Def.BalanceOfOutputDTO>)call).Output;
                        balances.Add(new TokenBalance
                        {
                            Address = meta.Address,
                            ContractAddress = meta.Contract,
                            Balance = output.ReturnValue1.ToString(),
                            TokenType = TokenTypeERC721,
                            LastUpdatedBlockNumber = blockNumber
                        });
                        break;
                    }
                    case TokenTypeERC721Owner:
                    {
                        var output = ((MulticallInputOutput<ERC721Def.OwnerOfFunction, ERC721Def.OwnerOfOutputDTO>)call).Output;
                        nftItems.Add(new NFTInventory
                        {
                            Address = output.ReturnValue1,
                            ContractAddress = meta.Contract,
                            TokenId = meta.TokenId,
                            Amount = "1",
                            TokenType = TokenTypeERC721,
                            LastUpdatedBlockNumber = blockNumber
                        });
                        break;
                    }
                    case TokenTypeERC1155:
                    {
                        var output = ((MulticallInputOutput<ERC1155Def.BalanceOfFunction, ERC1155Def.BalanceOfOutputDTO>)call).Output;
                        nftItems.Add(new NFTInventory
                        {
                            Address = meta.Address,
                            ContractAddress = meta.Contract,
                            TokenId = meta.TokenId,
                            Amount = output.ReturnValue1.ToString(),
                            TokenType = TokenTypeERC1155,
                            LastUpdatedBlockNumber = blockNumber
                        });
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex,
                    "Failed to decode {Type} for {Address} on {Contract} at block {Block}",
                    meta.TokenType, meta.Address, meta.Contract, blockNumber);
            }
        }

        private static async Task<List<(IMulticallInputOutput Call, bool HasError)>> ExecuteBatchAsync(
            IClient client,
            BlockParameter blockParam,
            List<IMulticallInputOutput> calls)
        {
            var handler = new MultiQueryBatchRpcHandler(client, defaultBlockParameter: blockParam);
            var batchItems = handler.CreateMulticallInputOutputRpcBatchItems(0, calls.ToArray());

            var rpcBatch = new RpcRequestResponseBatch { AcceptPartiallySuccessful = true };
            foreach (var item in batchItems)
                rpcBatch.BatchItems.Add(item.RpcRequestResponseBatchItem);

            await client.SendBatchRequestAsync(rpcBatch).ConfigureAwait(false);

            var results = new List<(IMulticallInputOutput Call, bool HasError)>(batchItems.Length);
            for (var i = 0; i < batchItems.Length; i++)
            {
                var item = batchItems[i];
                if (item.HasError)
                {
                    results.Add((item.MulticallInputOutput, true));
                    continue;
                }

                try
                {
                    var responseHex = item.RpcRequestResponseBatchItem.Response;
                    item.MulticallInputOutput.Decode(responseHex.HexToByteArray());
                    item.MulticallInputOutput.Success = true;
                    results.Add((item.MulticallInputOutput, false));
                }
                catch
                {
                    results.Add((item.MulticallInputOutput, true));
                }
            }

            return results;
        }

        private static List<(string Address, string Contract)> ExtractAccounts(
            IEnumerable<TokenTransferLog> logs, string tokenType)
        {
            return logs
                .Where(l => l.TokenType == tokenType)
                .SelectMany(l => new[]
                {
                    (Address: l.FromAddress?.ToLowerInvariant(), Contract: l.ContractAddress?.ToLowerInvariant()),
                    (Address: l.ToAddress?.ToLowerInvariant(), Contract: l.ContractAddress?.ToLowerInvariant())
                })
                .Where(x => !IsZeroAddress(x.Address))
                .Distinct()
                .ToList();
        }

        private static List<(string Address, string Contract, string TokenId)> ExtractErc1155Items(
            IEnumerable<TokenTransferLog> logs)
        {
            return logs
                .Where(l => l.TokenType == TokenTypeERC1155 && !string.IsNullOrEmpty(l.TokenId))
                .SelectMany(l => new[]
                {
                    (Address: l.FromAddress?.ToLowerInvariant(), Contract: l.ContractAddress?.ToLowerInvariant(), TokenId: l.TokenId),
                    (Address: l.ToAddress?.ToLowerInvariant(), Contract: l.ContractAddress?.ToLowerInvariant(), TokenId: l.TokenId)
                })
                .Where(x => !IsZeroAddress(x.Address))
                .Distinct()
                .ToList();
        }

        private static bool IsZeroAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return true;
            var normalized = address.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? address.Substring(2)
                : address;
            return normalized.Length == 0 || normalized.All(c => c == '0');
        }
    }
}
