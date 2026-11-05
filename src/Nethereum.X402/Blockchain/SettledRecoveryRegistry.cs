using System.Numerics;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;

namespace Nethereum.X402.Blockchain;

internal readonly record struct RecoveryScope(
    int ChainId, string Facilitator, BigInteger ExpiryUnixSeconds, IReadOnlyCollection<string> FacilitatorAddresses = null)
{
    public bool IsFacilitator(string sender)
    {
        if (sender == null)
            return false;

        if (sender.IsTheSameAddress(Facilitator))
            return true;

        return FacilitatorAddresses != null && FacilitatorAddresses.Any(address => sender.IsTheSameAddress(address));
    }
}

internal sealed record RecoveredSettlement(TransactionReceipt Receipt, string ClaimKey)
{
    public async Task<TransactionReceipt> ConfirmAsync(
        FacilitatorTransactionSender sender, IWeb3 web3, CancellationTokenSource receiptBound)
    {
        try
        {
            return await sender.ConfirmRecoveredAsync(web3, Receipt, receiptBound).ConfigureAwait(false);
        }
        catch
        {
            SettledRecoveryRegistry.Release(ClaimKey);
            throw;
        }
    }
}

internal static class SettledRecoveryRegistry
{
    public static void Release(string key)
    {
        lock (Gate)
        {
            Expiries.Remove(key);
        }
    }

    private const int MaxEntries = 10_000;

    private static readonly object Gate = new();
    private static readonly Dictionary<string, long> Expiries = new();
    private static readonly Queue<string> Order = new();

    public static bool TryClaim(string key, BigInteger expiryUnixSeconds)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiry = expiryUnixSeconds > long.MaxValue ? long.MaxValue : (long)BigInteger.Max(expiryUnixSeconds, now + 1);

        lock (Gate)
        {
            if (Expiries.TryGetValue(key, out var existing) && existing > now)
                return false;

            while (Order.Count > 0 && (Expiries.Count >= MaxEntries || Expiries.TryGetValue(Order.Peek(), out var oldest) && oldest <= now))
            {
                var evicted = Order.Dequeue();
                Expiries.Remove(evicted);
            }

            if (!Expiries.ContainsKey(key))
                Order.Enqueue(key);
            Expiries[key] = expiry;
            return true;
        }
    }
}
