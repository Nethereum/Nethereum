using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.Contracts.Standards.ERC721.ContractDefinition;
using Nethereum.Contracts.Standards.ERC1155.ContractDefinition;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Decoding;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.EVM.StateChanges
{
    public class StateChangesExtractor : IStateChangesExtractor
    {
        private static readonly EventABI ERC20TransferEvent = ABITypedRegistry.GetEvent<Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO>();
        private static readonly EventABI ERC721TransferEvent = ABITypedRegistry.GetEvent<Nethereum.Contracts.Standards.ERC721.ContractDefinition.TransferEventDTO>();
        private static readonly EventABI ERC1155TransferSingleEvent = ABITypedRegistry.GetEvent<TransferSingleEventDTO>();
        private static readonly EventABI ERC1155TransferBatchEvent = ABITypedRegistry.GetEvent<TransferBatchEventDTO>();

        public const string TRANSFER_EVENT_SIGNATURE = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";
        public const string TRANSFER_SINGLE_EVENT_SIGNATURE = "0xc3d58168c5ae7397731d063d5bbf3d657854427343f4c083240f7aacaa2d0f62";
        public const string TRANSFER_BATCH_EVENT_SIGNATURE = "0x4a39dc06d4c0dbc64b70af90fd698a233a518aa5d07e595d983b8c0526c8f7fb";

        public StateChangesResult ExtractFromDecodedResult(
            DecodedProgramResult decodedResult,
            ExecutionStateService stateService = null,
            string currentUserAddress = null)
        {
            return ExtractFromDecodedResult(decodedResult, stateService, currentUserAddress, null);
        }

        public StateChangesResult ExtractFromDecodedResult(
            DecodedProgramResult decodedResult,
            ExecutionStateService stateService,
            string currentUserAddress,
            Func<string, TokenInfo> tokenResolver)
        {
            if (decodedResult == null)
            {
                return new StateChangesResult { Error = "No decoded result provided" };
            }

            var result = new StateChangesResult
            {
                RootCall = decodedResult.RootCall,
                DecodedLogs = decodedResult.DecodedLogs ?? new List<DecodedLog>(),
                DecodedResult = decodedResult,
                BalanceChanges = new List<BalanceChange>()
            };

            ExtractTokenTransfersFromLogs(result, currentUserAddress, tokenResolver);
            ExtractEthTransfersFromCalls(result, currentUserAddress);

            if (stateService != null)
            {
                EnrichWithStateBalances(result, stateService);
            }

            result.BalanceChanges = ConsolidateBalanceChanges(result.BalanceChanges, currentUserAddress);

            return result;
        }

        public async Task<StateChangesResult> ExtractFromDecodedResultAsync(
            DecodedProgramResult decodedResult,
            ExecutionStateService stateService = null,
            string currentUserAddress = null,
            Func<string, Task<TokenInfo>> tokenResolverAsync = null,
            CancellationToken cancellationToken = default)
        {
            var result = ExtractFromDecodedResult(decodedResult, stateService, currentUserAddress, null);

            if (tokenResolverAsync != null && result.BalanceChanges != null)
            {
                var erc20Changes = result.BalanceChanges
                    .Where(c => c.Type == BalanceChangeType.ERC20 && !string.IsNullOrEmpty(c.TokenAddress))
                    .ToList();

                var tokenAddresses = erc20Changes
                    .Select(c => c.TokenAddress)
                    .Distinct()
                    .ToList();

                var tokenInfo = new Dictionary<string, TokenInfo>();
                foreach (var addr in tokenAddresses)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var info = await tokenResolverAsync(addr).ConfigureAwait(false);
                        if (info != null)
                        {
                            tokenInfo[addr.ToLowerInvariant()] = info;
                        }
                    }
                    catch
                    {
                    }
                }

                foreach (var change in erc20Changes)
                {
                    var key = change.TokenAddress?.ToLowerInvariant();
                    if (key != null && tokenInfo.TryGetValue(key, out var info))
                    {
                        change.TokenSymbol = info.Symbol;
                        change.TokenDecimals = info.Decimals;
                    }
                }
            }

            return result;
        }

        private void ExtractTokenTransfersFromLogs(StateChangesResult result, string currentUserAddress, Func<string, TokenInfo> tokenResolver)
        {
            if (result.DecodedLogs == null) return;

            foreach (var log in result.DecodedLogs)
            {
                var transferType = GetTransferEventType(log);
                if (transferType == TransferEventType.None) continue;

                var tokenAddress = log.ContractAddress;
                string tokenSymbol = null;
                int tokenDecimals = transferType == TransferEventType.ERC20 ? 18 : 0;

                if (tokenResolver != null && !string.IsNullOrEmpty(tokenAddress))
                {
                    try
                    {
                        var info = tokenResolver(tokenAddress);
                        if (info != null)
                        {
                            tokenSymbol = info.Symbol;
                            tokenDecimals = info.Decimals;
                        }
                    }
                    catch { }
                }

                if (transferType == TransferEventType.ERC1155Single)
                {
                    ExtractERC1155SingleTransfer(log, result.BalanceChanges, currentUserAddress, tokenAddress, tokenSymbol);
                }
                else if (transferType == TransferEventType.ERC1155Batch)
                {
                    ExtractERC1155BatchTransfer(log, result.BalanceChanges, currentUserAddress, tokenAddress, tokenSymbol);
                }
                else
                {
                    var transferParams = ExtractTransferParameters(log, transferType);
                    if (string.IsNullOrEmpty(transferParams.From) || string.IsNullOrEmpty(transferParams.To)) continue;

                    var balanceType = transferType == TransferEventType.ERC721 ? BalanceChangeType.ERC721 : BalanceChangeType.ERC20;

                    result.BalanceChanges.Add(new BalanceChange
                    {
                        Address = transferParams.From?.ToLowerInvariant(),
                        AddressLabel = log.ContractName,
                        Type = balanceType,
                        TokenAddress = tokenAddress,
                        TokenSymbol = tokenSymbol,
                        TokenDecimals = tokenDecimals,
                        TokenId = transferParams.TokenId,
                        Change = balanceType == BalanceChangeType.ERC721 ? -1 : -transferParams.Amount,
                        IsCurrentUser = transferParams.From.IsTheSameAddress(currentUserAddress)
                    });

                    result.BalanceChanges.Add(new BalanceChange
                    {
                        Address = transferParams.To?.ToLowerInvariant(),
                        Type = balanceType,
                        TokenAddress = tokenAddress,
                        TokenSymbol = tokenSymbol,
                        TokenDecimals = tokenDecimals,
                        TokenId = transferParams.TokenId,
                        Change = balanceType == BalanceChangeType.ERC721 ? 1 : transferParams.Amount,
                        IsCurrentUser = transferParams.To.IsTheSameAddress(currentUserAddress)
                    });
                }
            }
        }

        private void ExtractERC1155SingleTransfer(DecodedLog log, List<BalanceChange> changes, string currentUserAddress, string tokenAddress, string tokenSymbol)
        {
            var filterLog = log.OriginalLog;
            if (filterLog == null) return;

            var decoded = filterLog.DecodeEvent<TransferSingleEventDTO>();
            if (decoded == null) return;

            var from = decoded.Event.From;
            var to = decoded.Event.To;
            var tokenId = decoded.Event.Id;
            var amount = decoded.Event.Value;

            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;

            changes.Add(new BalanceChange
            {
                Address = from?.ToLowerInvariant(),
                AddressLabel = log.ContractName,
                Type = BalanceChangeType.ERC1155,
                TokenAddress = tokenAddress,
                TokenSymbol = tokenSymbol,
                TokenId = tokenId,
                Change = -amount,
                IsCurrentUser = from.IsTheSameAddress(currentUserAddress)
            });

            changes.Add(new BalanceChange
            {
                Address = to?.ToLowerInvariant(),
                Type = BalanceChangeType.ERC1155,
                TokenAddress = tokenAddress,
                TokenSymbol = tokenSymbol,
                TokenId = tokenId,
                Change = amount,
                IsCurrentUser = to.IsTheSameAddress(currentUserAddress)
            });
        }

        private void ExtractERC1155BatchTransfer(DecodedLog log, List<BalanceChange> changes, string currentUserAddress, string tokenAddress, string tokenSymbol)
        {
            var filterLog = log.OriginalLog;
            if (filterLog == null) return;

            var decoded = filterLog.DecodeEvent<TransferBatchEventDTO>();
            if (decoded == null) return;

            var from = decoded.Event.From;
            var to = decoded.Event.To;
            var tokenIds = decoded.Event.Ids;
            var amounts = decoded.Event.Values;

            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || tokenIds == null || tokenIds.Count == 0) return;

            var fromLower = from?.ToLowerInvariant();
            var toLower = to?.ToLowerInvariant();
            var isFromCurrentUser = from.IsTheSameAddress(currentUserAddress);
            var isToCurrentUser = to.IsTheSameAddress(currentUserAddress);

            for (int i = 0; i < tokenIds.Count; i++)
            {
                var amount = (amounts != null && i < amounts.Count) ? amounts[i] : BigInteger.One;

                changes.Add(new BalanceChange
                {
                    Address = fromLower,
                    AddressLabel = log.ContractName,
                    Type = BalanceChangeType.ERC1155,
                    TokenAddress = tokenAddress,
                    TokenSymbol = tokenSymbol,
                    TokenId = tokenIds[i],
                    Change = -amount,
                    IsCurrentUser = isFromCurrentUser
                });

                changes.Add(new BalanceChange
                {
                    Address = toLower,
                    Type = BalanceChangeType.ERC1155,
                    TokenAddress = tokenAddress,
                    TokenSymbol = tokenSymbol,
                    TokenId = tokenIds[i],
                    Change = amount,
                    IsCurrentUser = isToCurrentUser
                });
            }
        }

        private void ExtractEthTransfersFromCalls(StateChangesResult result, string currentUserAddress)
        {
            if (result.RootCall == null) return;

            ExtractEthTransfersRecursive(result.RootCall, result.BalanceChanges, currentUserAddress);
        }

        private void ExtractEthTransfersRecursive(DecodedCall call, List<BalanceChange> changes, string currentUserAddress)
        {
            if (call.Value > 0)
            {
                changes.Add(new BalanceChange
                {
                    Address = call.From?.ToLowerInvariant(),
                    Type = BalanceChangeType.Native,
                    Change = -call.Value,
                    IsCurrentUser = call.From.IsTheSameAddress(currentUserAddress)
                });

                changes.Add(new BalanceChange
                {
                    Address = call.To?.ToLowerInvariant(),
                    AddressLabel = call.ContractName,
                    Type = BalanceChangeType.Native,
                    Change = call.Value,
                    IsCurrentUser = call.To.IsTheSameAddress(currentUserAddress)
                });
            }

            if (call.InnerCalls != null)
            {
                foreach (var innerCall in call.InnerCalls)
                {
                    ExtractEthTransfersRecursive(innerCall, changes, currentUserAddress);
                }
            }
        }

        private void EnrichWithStateBalances(StateChangesResult result, ExecutionStateService stateService)
        {
            if (stateService?.AccountsState == null) return;

            foreach (var change in result.BalanceChanges.Where(c => c.Type == BalanceChangeType.Native))
            {
                var address = change.Address?.ToLowerInvariant();
                if (string.IsNullOrEmpty(address)) continue;

                if (stateService.AccountsState.TryGetValue(address, out var accountState))
                {
                    var balance = accountState.Balance;
                    if (balance != null)
                    {
                        change.BalanceBefore = balance.InitialChainBalance;
                        change.BalanceAfter = balance.GetTotalBalance();

                        if (change.BalanceAfter.HasValue && change.BalanceBefore.HasValue)
                        {
                            var actualChange = change.BalanceAfter.Value - change.BalanceBefore.Value;
                            change.ActualChange = actualChange;

                            if (actualChange == change.Change)
                            {
                                change.ValidationStatus = BalanceValidationStatus.Verified;
                            }
                            else
                            {
                                change.ValidationStatus = BalanceValidationStatus.Mismatch;
                            }
                        }
                    }
                }
            }
        }

        public void ValidateTokenBalances(
            StateChangesResult result,
            Program program,
            ExecutionStateService stateService,
            Func<string, string, BigInteger> getErc20Balance = null,
            Func<string, BigInteger, string> getErc721Owner = null,
            Func<string, string, BigInteger, BigInteger> getErc1155Balance = null)
        {
            if (result?.BalanceChanges == null) return;

            foreach (var change in result.BalanceChanges)
            {
                if (change.ValidationStatus != BalanceValidationStatus.NotValidated)
                    continue;

                try
                {
                    switch (change.Type)
                    {
                        case BalanceChangeType.ERC20:
                            if (getErc20Balance != null && change.BalanceBefore.HasValue && change.BalanceAfter.HasValue)
                            {
                                var actualChange = change.BalanceAfter.Value - change.BalanceBefore.Value;
                                change.ActualChange = actualChange;

                                if (actualChange == change.Change)
                                {
                                    change.ValidationStatus = BalanceValidationStatus.Verified;
                                }
                                else if (BigInteger.Abs(actualChange) < BigInteger.Abs(change.Change))
                                {
                                    change.ValidationStatus = BalanceValidationStatus.FeeOnTransfer;
                                }
                                else if (BigInteger.Abs(actualChange) > BigInteger.Abs(change.Change))
                                {
                                    change.ValidationStatus = BalanceValidationStatus.Rebasing;
                                }
                                else
                                {
                                    change.ValidationStatus = BalanceValidationStatus.Mismatch;
                                }
                            }
                            break;

                        case BalanceChangeType.ERC721:
                            if (getErc721Owner != null && change.TokenId.HasValue)
                            {
                                var actualOwner = getErc721Owner(change.TokenAddress, change.TokenId.Value);
                                change.ActualOwner = actualOwner;

                                if (change.Change > 0)
                                {
                                    change.ValidationStatus = actualOwner?.Equals(change.Address, StringComparison.OrdinalIgnoreCase) == true
                                        ? BalanceValidationStatus.Verified
                                        : BalanceValidationStatus.OwnerMismatch;
                                }
                                else
                                {
                                    change.ValidationStatus = actualOwner?.Equals(change.Address, StringComparison.OrdinalIgnoreCase) != true
                                        ? BalanceValidationStatus.Verified
                                        : BalanceValidationStatus.OwnerMismatch;
                                }
                            }
                            break;

                        case BalanceChangeType.ERC1155:
                            if (getErc1155Balance != null && change.TokenId.HasValue && change.BalanceBefore.HasValue && change.BalanceAfter.HasValue)
                            {
                                var actualChange = change.BalanceAfter.Value - change.BalanceBefore.Value;
                                change.ActualChange = actualChange;

                                change.ValidationStatus = actualChange == change.Change
                                    ? BalanceValidationStatus.Verified
                                    : BalanceValidationStatus.Mismatch;
                            }
                            break;
                    }
                }
                catch
                {
                }
            }
        }

        private List<BalanceChange> ConsolidateBalanceChanges(List<BalanceChange> changes, string currentUserAddress)
        {
            if (changes == null || changes.Count == 0) return new List<BalanceChange>();

            var grouped = new List<BalanceChange>();
            var groupKeys = changes
                .Select(c => new
                {
                    Address = c.Address?.ToLowerInvariant(),
                    c.Type,
                    TokenAddress = c.TokenAddress?.ToLowerInvariant(),
                    TokenId = (c.Type == BalanceChangeType.ERC721 || c.Type == BalanceChangeType.ERC1155) ? c.TokenId : null
                })
                .Distinct()
                .ToList();

            foreach (var key in groupKeys)
            {
                var groupItems = changes
                    .Where(c =>
                        c.Address?.ToLowerInvariant() == key.Address &&
                        c.Type == key.Type &&
                        c.TokenAddress?.ToLowerInvariant() == key.TokenAddress &&
                        ((c.Type != BalanceChangeType.ERC721 && c.Type != BalanceChangeType.ERC1155) || c.TokenId == key.TokenId))
                    .ToList();

                if (groupItems.Count == 0) continue;

                var first = groupItems[0];
                BigInteger totalChange = BigInteger.Zero;
                foreach (var item in groupItems)
                {
                    totalChange += item.Change;
                }

                if (totalChange == 0) continue;

                grouped.Add(new BalanceChange
                {
                    Address = first.Address,
                    AddressLabel = groupItems.Select(c => c.AddressLabel).FirstOrDefault(l => !string.IsNullOrEmpty(l)),
                    Type = first.Type,
                    TokenAddress = first.TokenAddress,
                    TokenSymbol = groupItems.Select(c => c.TokenSymbol).FirstOrDefault(s => !string.IsNullOrEmpty(s)),
                    TokenDecimals = groupItems.Max(c => c.TokenDecimals),
                    TokenId = first.TokenId,
                    Change = totalChange,
                    BalanceBefore = groupItems.Select(c => c.BalanceBefore).FirstOrDefault(b => b.HasValue),
                    BalanceAfter = groupItems.Select(c => c.BalanceAfter).FirstOrDefault(b => b.HasValue),
                    ActualChange = groupItems.Select(c => c.ActualChange).FirstOrDefault(a => a.HasValue),
                    ActualOwner = groupItems.Select(c => c.ActualOwner).FirstOrDefault(o => !string.IsNullOrEmpty(o)),
                    ValidationStatus = groupItems.Select(c => c.ValidationStatus)
                        .FirstOrDefault(s => s != BalanceValidationStatus.NotValidated),
                    IsCurrentUser = first.Address.IsTheSameAddress(currentUserAddress)
                });
            }

            return grouped
                .OrderByDescending(c => c.IsCurrentUser)
                .ThenBy(c => c.Type)
                .ThenBy(c => c.Address)
                .ToList();
        }

        private TransferEventType GetTransferEventType(DecodedLog log)
        {
            if (log == null) return TransferEventType.None;

            var filterLog = log.OriginalLog;
            if (filterLog == null) return TransferEventType.None;

            if (ERC1155TransferSingleEvent.IsLogForEvent(filterLog))
                return TransferEventType.ERC1155Single;

            if (ERC1155TransferBatchEvent.IsLogForEvent(filterLog))
                return TransferEventType.ERC1155Batch;

            if (ERC721TransferEvent.IsLogForEvent(filterLog))
                return TransferEventType.ERC721;

            if (ERC20TransferEvent.IsLogForEvent(filterLog))
                return TransferEventType.ERC20;

            return TransferEventType.None;
        }

        private TransferParameters ExtractTransferParameters(DecodedLog log, TransferEventType eventType = TransferEventType.ERC20)
        {
            var result = new TransferParameters();
            var filterLog = log.OriginalLog;

            if (filterLog != null && eventType == TransferEventType.ERC721)
            {
                var decoded = filterLog.DecodeEvent<Nethereum.Contracts.Standards.ERC721.ContractDefinition.TransferEventDTO>();
                if (decoded != null)
                {
                    result.From = decoded.Event.From;
                    result.To = decoded.Event.To;
                    result.TokenId = decoded.Event.TokenId;
                    result.Amount = BigInteger.One;
                    return result;
                }
            }

            if (filterLog != null && eventType == TransferEventType.ERC20)
            {
                var decoded = filterLog.DecodeEvent<Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO>();
                if (decoded != null)
                {
                    result.From = decoded.Event.From;
                    result.To = decoded.Event.To;
                    result.Amount = decoded.Event.Value;
                    return result;
                }
            }

            return result;
        }

        private class TransferParameters
        {
            public string From { get; set; }
            public string To { get; set; }
            public BigInteger Amount { get; set; }
            public BigInteger? TokenId { get; set; }
        }

        private enum TransferEventType
        {
            None,
            ERC20,
            ERC721,
            ERC1155Single,
            ERC1155Batch
        }
    }
}
