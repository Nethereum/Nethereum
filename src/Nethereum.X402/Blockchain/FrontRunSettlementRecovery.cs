using System.Numerics;
using Nethereum.Contracts;
using Nethereum.Util;
using Nethereum.X402.Permit2;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Contracts.EIP3009.EIP3009;
using Nethereum.Contracts.Standards.Permit2;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace Nethereum.X402.Blockchain;

internal static class FrontRunSettlementRecovery
{
    private const string AuthorizationUsedTopic = "0x98de503528ee59b575ef0c0a2576a82497bfc029a5685b209e9ec333479b10a5";
    private const string TransferTopic = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";

    public static async Task<RecoveredSettlement?> FindEip3009SettlementAsync(
        IWeb3 web3,
        string asset,
        string payer,
        string nonce,
        string payTo,
        BigInteger amount,
        BigInteger sinceBlock,
        RecoveryScope scope)
    {
        if (!X402Address.TryCanonical(payer, out var canonicalPayer))
            return null;

        var nonceBytes = nonce.HexToByteArray();
        if (!await new Eip3009Service(web3, asset).AuthorizationStateQueryAsync(payer, nonceBytes).ConfigureAwait(false))
            return null;

        var logs = await GetLogsAsync(
            web3, asset, sinceBlock, AuthorizationUsedTopic, AddressTopic(canonicalPayer), nonceBytes.ToHex(true)).ConfigureAwait(false);
        return await FirstSettlementAsync(
            web3, logs, asset, payer, payTo, amount, scope,
            $"{scope.ChainId}:{asset.ToLowerInvariant()}:{canonicalPayer}:{nonceBytes.ToHex(true)}",
            (_, receipt) => AuthorizationUsedIsFollowedByTheTransfer(receipt, asset, canonicalPayer, nonceBytes, payTo, amount)).ConfigureAwait(false);
    }

    public static async Task<RecoveredSettlement?> FindPermit2SettlementAsync(
        IWeb3 web3,
        string asset,
        string payer,
        BigInteger nonce,
        string payTo,
        BigInteger amount,
        BigInteger sinceBlock,
        RecoveryScope scope)
    {
        if (!X402Address.TryCanonical(payer, out var canonicalPayer) || !X402Address.TryCanonical(payTo, out var canonicalPayTo))
            return null;

        if (!await IsPermit2NonceUsedAsync(web3, payer, nonce).ConfigureAwait(false))
            return null;

        var logs = await GetLogsAsync(
            web3, asset, sinceBlock, TransferTopic, AddressTopic(canonicalPayer), AddressTopic(canonicalPayTo)).ConfigureAwait(false);
        return await FirstSettlementAsync(
            web3, logs, asset, payer, payTo, amount, scope,
            $"{scope.ChainId}:{asset.ToLowerInvariant()}:{canonicalPayer}:{nonce}",
            (transaction, _) => IsTheProxySettlingThisPermit(transaction, asset, payer, nonce, payTo, amount)).ConfigureAwait(false);
    }

    internal static async Task<bool> IsPermit2NonceUsedAsync(IWeb3 web3, string payer, BigInteger nonce)
    {
        var bitmap = await web3.Eth.GetPermit2Service().NonceBitmapQueryAsync(payer, nonce >> 8).ConfigureAwait(false);
        return ((bitmap >> (int)(nonce & 0xff)) & BigInteger.One) == BigInteger.One;
    }

    private static async Task<FilterLog[]> GetLogsAsync(
        IWeb3 web3, string asset, BigInteger sinceBlock, string topic0, string topic1, string topic2)
    {
        var filter = new NewFilterInput
        {
            Address = new[] { asset },
            FromBlock = new BlockParameter(new HexBigInteger(sinceBlock)),
            ToBlock = BlockParameter.CreateLatest(),
            Topics = new object[] { topic0, topic1, topic2 }
        };
        return await web3.Eth.Filters.GetLogs.SendRequestAsync(filter).ConfigureAwait(false) ?? Array.Empty<FilterLog>();
    }

    private static async Task<RecoveredSettlement?> FirstSettlementAsync(
        IWeb3 web3,
        FilterLog[] logs,
        string asset,
        string payer,
        string payTo,
        BigInteger amount,
        RecoveryScope scope,
        string registryKey,
        Func<Transaction?, TransactionReceipt, bool> bindsToThisAuthorization)
    {
        foreach (var hash in logs.Select(log => log.TransactionHash).Distinct())
        {
            var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(hash).ConfigureAwait(false);
            if (receipt?.Status?.Value != 1 ||
                !SettlementTransferCheck.HasExactlyOneMatchingTransfer(receipt, asset, payer, payTo, amount))
                continue;

            var transaction = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(hash).ConfigureAwait(false);
            if (scope.IsFacilitator(transaction?.From))
                continue;

            if (!bindsToThisAuthorization(transaction, receipt))
                continue;

            return SettledRecoveryRegistry.TryClaim(registryKey, scope.ExpiryUnixSeconds)
                ? new RecoveredSettlement(receipt, registryKey)
                : null;
        }

        return null;
    }

    private static bool AuthorizationUsedIsFollowedByTheTransfer(
        TransactionReceipt receipt, string asset, string canonicalPayer, byte[] nonce, string payTo, BigInteger amount)
    {
        var logs = receipt.Logs ?? Array.Empty<FilterLog>();
        var nonceTopic = nonce.ToHex(true);
        foreach (var used in logs)
        {
            if (!IsAssetLog(used, asset, AuthorizationUsedTopic, 3)
                || !SameWord(used.Topics[1], AddressTopic(canonicalPayer))
                || !SameWord(used.Topics[2], nonceTopic)
                || used.LogIndex == null)
                continue;

            var next = logs.FirstOrDefault(log => log.LogIndex != null && log.LogIndex.Value == used.LogIndex.Value + 1);
            if (next != null
                && IsAssetLog(next, asset, TransferTopic, 3)
                && SameWord(next.Topics[1], AddressTopic(canonicalPayer))
                && X402Address.TryCanonical(payTo, out var canonicalPayTo)
                && SameWord(next.Topics[2], AddressTopic(canonicalPayTo))
                && TransferValue(next) == amount)
                return true;
        }

        return false;
    }

    private static bool IsTheProxySettlingThisPermit(
        Transaction? transaction, string asset, string payer, BigInteger nonce, string payTo, BigInteger amount)
    {
        if (transaction?.To == null || !transaction.To.IsTheSameAddress(X402Permit2Addresses.ExactPermit2Proxy))
            return false;

        if (transaction.IsTransactionForFunctionMessage<SettleFunction>())
        {
            var settle = transaction.DecodeTransactionToFunctionMessage<SettleFunction>();
            return MatchesPermit(settle.Permit, settle.Owner, settle.Witness, asset, payer, nonce, payTo, amount);
        }

        if (transaction.IsTransactionForFunctionMessage<SettleWithPermitFunction>())
        {
            var settle = transaction.DecodeTransactionToFunctionMessage<SettleWithPermitFunction>();
            return MatchesPermit(settle.Permit, settle.Owner, settle.Witness, asset, payer, nonce, payTo, amount);
        }

        return false;
    }

    private static bool MatchesPermit(
        PermitTransferFrom permit, string owner, Witness witness,
        string asset, string payer, BigInteger nonce, string payTo, BigInteger amount) =>
        permit?.Permitted != null
        && witness != null
        && owner.IsTheSameAddress(payer)
        && permit.Permitted.Token.IsTheSameAddress(asset)
        && permit.Nonce == nonce
        && permit.Permitted.Amount == amount
        && witness.To.IsTheSameAddress(payTo);

    private static bool IsAssetLog(FilterLog log, string asset, string topic0, int topicCount) =>
        log.Address.IsTheSameAddress(asset)
        && log.Topics != null
        && log.Topics.Length == topicCount
        && string.Equals(log.Topics[0]?.ToString(), topic0, StringComparison.OrdinalIgnoreCase);

    private static bool SameWord(object? topic, string expected) =>
        string.Equals(topic?.ToString(), expected, StringComparison.OrdinalIgnoreCase);

    private static BigInteger TransferValue(FilterLog log) =>
        string.IsNullOrEmpty(log.Data)
            ? BigInteger.MinusOne
            : new BigInteger(log.Data.HexToByteArray(), isUnsigned: true, isBigEndian: true);

    private static string AddressTopic(string canonicalAddress) => "0x" + canonicalAddress.Substring(2).PadLeft(64, '0');
}
