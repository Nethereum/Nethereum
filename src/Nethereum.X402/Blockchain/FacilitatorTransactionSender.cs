using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.X402.Facilitator;

namespace Nethereum.X402.Blockchain;

internal sealed class FacilitatorTransactionSender
{
    private const int DefaultTimeoutSeconds = 60;
    private const int GasHeadroomPercent = 120;
    private const int ConfirmationPollMilliseconds = 250;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SendLocks = new();

    private readonly X402FacilitatorOptions _options;

    public FacilitatorTransactionSender(X402FacilitatorOptions? options = null)
    {
        _options = options ?? new X402FacilitatorOptions();
    }

    public static TimeSpan BoundFor(int maxTimeoutSeconds, int maxWaitSeconds = X402FacilitatorOptions.DefaultMaxReceiptWaitSeconds) =>
        TimeSpan.FromSeconds(Math.Clamp(maxTimeoutSeconds > 0 ? maxTimeoutSeconds : DefaultTimeoutSeconds, 1, Math.Max(1, maxWaitSeconds)));

    public TimeSpan Bound(int maxTimeoutSeconds) => BoundFor(maxTimeoutSeconds, _options.MaxReceiptWaitSeconds);

    public BigInteger GasLimitFor(BigInteger estimate)
    {
        if (estimate > _options.MaxSettlementGas)
            throw new X402SettlementGasExceededException(estimate, _options.MaxSettlementGas);

        return BigInteger.Min(_options.MaxSettlementGas, estimate * GasHeadroomPercent / 100);
    }

    public async Task<TransactionReceipt> SendAndWaitForReceiptAsync<TFunction>(
        IWeb3 web3,
        IContractHandler contractHandler,
        TFunction function,
        CancellationTokenSource receiptBound)
        where TFunction : FunctionMessage, new()
    {
        var settlementBound = receiptBound as SettlementBound;
        var transactionHash = await SendUnderLockAsync(
            web3,
            settlementBound?.Duration,
            CancellationToken.None,
            () =>
            {
                settlementBound?.Start();
                return SendFunctionAsync(web3, contractHandler, function);
            }).ConfigureAwait(false);

        return await WaitForReceiptAsync(web3, transactionHash, receiptBound.Token, _options.RequiredConfirmations).ConfigureAwait(false);
    }

    public async Task<TransactionReceipt> ConfirmRecoveredAsync(
        IWeb3 web3,
        TransactionReceipt found,
        CancellationTokenSource receiptBound)
    {
        (receiptBound as SettlementBound)?.Start();
        try
        {
            return await RaceAgainstBoundAsync(
                ConfirmAsync(web3, found.TransactionHash, found, receiptBound.Token, _options.RequiredConfirmations),
                receiptBound.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new X402SettlementPendingException(found.TransactionHash, ex);
        }
    }

    private static async Task AcquireAsync(SemaphoreSlim gate, TimeSpan? lockBound, CancellationToken cancellationToken)
    {
        if (lockBound == null)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!await gate.WaitAsync(lockBound.Value, cancellationToken).ConfigureAwait(false))
            throw new TimeoutException("Waiting for the facilitator send lock exceeded its bound; nothing was broadcast.");
    }

    private static async Task<string> SendUnderLockAsync(
        IWeb3 web3,
        TimeSpan? lockBound,
        CancellationToken lockToken,
        Func<Task<string>> send)
    {
        var gate = await LockForAsync(web3).ConfigureAwait(false);
        await AcquireAsync(gate, lockBound, lockToken).ConfigureAwait(false);
        try
        {
            return await send().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<string> BroadcastAsync(Func<Task<string>> send)
    {
        try
        {
            return await send().ConfigureAwait(false);
        }
        catch (Exception ex) when (X402SettleOutcomes.BroadcastOutcomeIsUnknown(ex))
        {
            throw new X402SettlementPendingException(string.Empty, ex);
        }
    }

    private async Task<string> SendFunctionAsync<TFunction>(
        IWeb3 web3,
        IContractHandler contractHandler,
        TFunction function)
        where TFunction : FunctionMessage, new()
    {
        function.Nonce = await PendingNonceAsync(web3).ConfigureAwait(false);
        if (function.Gas == null)
            function.Gas = GasLimitFor((await contractHandler.EstimateGasAsync(function).ConfigureAwait(false)).Value);
        return await BroadcastAsync(() => contractHandler.SendRequestAsync(function)).ConfigureAwait(false);
    }

    public async Task<TransactionReceipt> SendAndWaitForReceiptAsync<TFunction>(
        IWeb3 web3,
        IContractHandler contractHandler,
        TFunction function,
        CancellationToken receiptBound)
        where TFunction : FunctionMessage, new()
    {
        var transactionHash = await SendUnderLockAsync(
            web3,
            null,
            receiptBound,
            () => SendFunctionAsync(web3, contractHandler, function)).ConfigureAwait(false);

        return await WaitForReceiptAsync(web3, transactionHash, receiptBound, _options.RequiredConfirmations).ConfigureAwait(false);
    }

    public async Task<TransactionReceipt> TransferAndWaitForReceiptAsync(
        IWeb3 web3,
        string to,
        BigInteger value,
        int maxTimeoutSeconds)
    {
        var transactionHash = await SendUnderLockAsync(
            web3,
            Bound(maxTimeoutSeconds),
            CancellationToken.None,
            async () =>
            {
                var account = web3.TransactionManager.Account;
                var transfer = new TransactionInput
                {
                    From = account.Address,
                    To = to,
                    Value = new HexBigInteger(value),
                    Nonce = new HexBigInteger(await PendingNonceAsync(web3).ConfigureAwait(false))
                };
                var estimate = await web3.TransactionManager.EstimateGasAsync(transfer).ConfigureAwait(false);
                transfer.Gas = new HexBigInteger(GasLimitFor(estimate.Value));
                return await BroadcastAsync(() => web3.TransactionManager.SendTransactionAsync(transfer)).ConfigureAwait(false);
            }).ConfigureAwait(false);

        return await WaitForReceiptAsync(web3, transactionHash, maxTimeoutSeconds, _options).ConfigureAwait(false);
    }

    public Task<TransactionReceipt> AwaitReceiptAsync(
        IWeb3 web3,
        string transactionHash,
        int maxTimeoutSeconds) =>
        WaitForReceiptAsync(web3, transactionHash, maxTimeoutSeconds, _options);

    public static async Task<TransactionReceipt> WaitForReceiptAsync(
        IWeb3 web3,
        string transactionHash,
        int maxTimeoutSeconds,
        X402FacilitatorOptions? options)
    {
        try
        {
            var maxWait = options?.MaxReceiptWaitSeconds ?? X402FacilitatorOptions.DefaultMaxReceiptWaitSeconds;
            using var bound = new CancellationTokenSource(BoundFor(maxTimeoutSeconds, maxWait));
            return await WaitForReceiptAsync(web3, transactionHash, bound.Token, options?.RequiredConfirmations ?? 1).ConfigureAwait(false);
        }
        catch (X402SettlementPendingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new X402SettlementPendingException(transactionHash, ex);
        }
    }

    public static async Task<TransactionReceipt> WaitForReceiptAsync(
        IWeb3 web3,
        string transactionHash,
        CancellationToken receiptBound,
        int requiredConfirmations = 1)
    {
        try
        {
            return await RaceAgainstBoundAsync(
                ReceiptWithConfirmationsAsync(web3, transactionHash, receiptBound, requiredConfirmations),
                receiptBound).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new X402SettlementPendingException(transactionHash, ex);
        }
    }

    private static async Task<TransactionReceipt> RaceAgainstBoundAsync(Task<TransactionReceipt> confirmed, CancellationToken receiptBound)
    {
        var expired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = receiptBound.Register(() => expired.TrySetResult(true));
        if (await Task.WhenAny(confirmed, expired.Task).ConfigureAwait(false) != confirmed)
        {
            _ = confirmed.ContinueWith(t => t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            throw new TimeoutException("The receipt wait bound expired.");
        }

        return await confirmed.ConfigureAwait(false);
    }

    private static async Task<TransactionReceipt> ReceiptWithConfirmationsAsync(
        IWeb3 web3,
        string transactionHash,
        CancellationToken receiptBound,
        int requiredConfirmations)
    {
        var receipt = await web3.TransactionReceiptPolling.PollForReceiptAsync(transactionHash, receiptBound).ConfigureAwait(false);
        return await ConfirmAsync(web3, transactionHash, receipt, receiptBound, requiredConfirmations).ConfigureAwait(false);
    }

    private static async Task<TransactionReceipt> ConfirmAsync(
        IWeb3 web3,
        string transactionHash,
        TransactionReceipt receipt,
        CancellationToken receiptBound,
        int requiredConfirmations)
    {
        if (requiredConfirmations <= 1)
            return receipt;

        while (true)
        {
            var head = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync().ConfigureAwait(false);
            if (head.Value - receipt.BlockNumber.Value + 1 >= requiredConfirmations)
                break;

            await Task.Delay(ConfirmationPollMilliseconds, receiptBound).ConfigureAwait(false);
        }

        var current = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash).ConfigureAwait(false);
        if (current == null || current.BlockHash != receipt.BlockHash)
            throw new InvalidOperationException("The settlement transaction was reorganised out of the block that confirmed it.");

        return current;
    }

    private static async Task<SemaphoreSlim> LockForAsync(IWeb3 web3)
    {
        var chainId = web3.TransactionManager.ChainId
            ?? (await web3.Eth.ChainId.SendRequestAsync().ConfigureAwait(false)).Value;
        var key = $"{web3.TransactionManager.Account.Address.ToLowerInvariant()}:{chainId}";
        return SendLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }

    private static async Task<BigInteger> PendingNonceAsync(IWeb3 web3)
    {
        var count = await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(
            web3.TransactionManager.Account.Address, BlockParameter.CreatePending()).ConfigureAwait(false);
        return count.Value;
    }
}
