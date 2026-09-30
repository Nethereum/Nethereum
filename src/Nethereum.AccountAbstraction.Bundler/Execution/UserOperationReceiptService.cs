using System.Numerics;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.AccountAbstraction.DTOs;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Bundler.Execution
{
    public class UserOperationReceiptService
    {
        private readonly IWeb3 _web3;

        public UserOperationReceiptService(IWeb3 web3)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
        }

        public async Task<UserOperationReceipt?> FindByLogScanAsync(
            string userOpHash, string entryPoint, BigInteger lookbackBlocks)
        {
            var operationEvent = _web3.Eth.GetEvent<UserOperationEventEventDTO>(entryPoint);

            BlockParameter fromBlock;
            if (lookbackBlocks == 0)
            {
                fromBlock = BlockParameter.CreateEarliest();
            }
            else
            {
                var latest = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                var from = BigInteger.Max(0, latest.Value - lookbackBlocks);
                fromBlock = new BlockParameter(new HexBigInteger(from));
            }

            var filter = operationEvent.CreateFilterInput(
                new object[] { userOpHash.HexToByteArray() },
                fromBlock,
                BlockParameter.CreateLatest());

            var matches = await operationEvent.GetAllChangesAsync(filter);
            var match = matches.FirstOrDefault();
            if (match == null) return null;

            var transactionReceipt = await _web3.Eth.Transactions.GetTransactionReceipt
                .SendRequestAsync(match.Log.TransactionHash);
            if (transactionReceipt == null) return null;

            return BuildFromTransactionReceipt(transactionReceipt, userOpHash, entryPoint);
        }

        public UserOperationReceipt? BuildFromTransactionReceipt(
            TransactionReceipt transactionReceipt, string userOpHash, string entryPoint)
        {
            var operationEvents = transactionReceipt.DecodeAllEvents<UserOperationEventEventDTO>()
                .Where(e => IsFromEntryPoint(e.Log, entryPoint))
                .ToList();

            var target = operationEvents.FirstOrDefault(e => MatchesHash(e.Event.UserOpHash, userOpHash));
            if (target?.Log?.LogIndex == null) return null;

            var targetIndex = target.Log.LogIndex.Value;

            var lowerBound = BigInteger.MinusOne;

            foreach (var beforeExecution in transactionReceipt.DecodeAllEvents<BeforeExecutionEventDTO>()
                         .Where(e => IsFromEntryPoint(e.Log, entryPoint)))
            {
                var index = beforeExecution.Log?.LogIndex?.Value ?? BigInteger.MinusOne;
                if (index < targetIndex && index > lowerBound) lowerBound = index;
            }

            foreach (var otherEvent in operationEvents)
            {
                var index = otherEvent.Log?.LogIndex?.Value ?? BigInteger.MinusOne;
                if (index < targetIndex && index > lowerBound) lowerBound = index;
            }

            var logs = transactionReceipt.Logs
                .Where(log => log?.LogIndex != null &&
                              log.LogIndex.Value > lowerBound &&
                              log.LogIndex.Value <= targetIndex)
                .OrderBy(log => log.LogIndex!.Value)
                .ToList();

            string? reason = null;
            var revert = transactionReceipt.DecodeAllEvents<UserOperationRevertReasonEventDTO>()
                .FirstOrDefault(e => IsFromEntryPoint(e.Log, entryPoint) &&
                                     MatchesHash(e.Event.UserOpHash, userOpHash));
            if (revert != null)
            {
                reason = RevertReasonDecoder.Decode(revert.Event.RevertReason);
            }

            return new UserOperationReceipt
            {
                UserOpHash = userOpHash,
                EntryPoint = entryPoint,
                Sender = target.Event.Sender,
                Nonce = new HexBigInteger(target.Event.Nonce),
                Paymaster = target.Event.Paymaster,
                ActualGasCost = new HexBigInteger(target.Event.ActualGasCost),
                ActualGasUsed = new HexBigInteger(target.Event.ActualGasUsed),
                Success = target.Event.Success,
                Reason = reason,
                Logs = logs,
                Receipt = transactionReceipt
            };
        }

        private static bool MatchesHash(byte[]? eventHash, string userOpHash)
        {
            return eventHash != null &&
                   eventHash.ToHex().Equals(
                       userOpHash.RemoveHexPrefix(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFromEntryPoint(FilterLog? log, string entryPoint)
        {
            return log?.Address != null &&
                   log.Address.Equals(entryPoint, StringComparison.OrdinalIgnoreCase);
        }
    }
}
