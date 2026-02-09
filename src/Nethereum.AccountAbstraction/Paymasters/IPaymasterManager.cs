using System.Numerics;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;

namespace Nethereum.AccountAbstraction.Paymasters
{
    public interface IPaymasterManager
    {
        string Address { get; }
        string EntryPointAddress { get; }

        Task<SponsorResult> SponsorUserOperationAsync(PackedUserOperation userOp, SponsorContext? context = null);
        Task<BigInteger> GetDepositAsync();
        Task<TransactionReceipt> DepositAsync(BigInteger amount);
        Task<TransactionReceipt> WithdrawToAsync(string to, BigInteger amount);
    }

    public interface IVerifyingPaymasterManager : IPaymasterManager
    {
        Task<SponsorResult> SponsorWithSignatureAsync(PackedUserOperation userOp, ulong validUntil, ulong validAfter, EthECKey signerKey);
        Task<byte[]> GetHashAsync(PackedUserOperation userOp, ulong validUntil, ulong validAfter);
    }

    public interface IDepositPaymasterManager : IPaymasterManager
    {
        Task<BigInteger> GetUserDepositAsync(string account);
        Task<TransactionReceipt> DepositForAsync(string account, BigInteger amount);
        Task<TransactionReceipt> WithdrawFromAsync(string account, BigInteger amount);
    }

    public interface ITokenPaymasterManager : IPaymasterManager
    {
        Task<string> GetTokenAddressAsync();
        Task<BigInteger> EstimateTokenCostAsync(BigInteger ethCost);
        Task<BigInteger> GetTokenBalanceAsync(string account);
    }
}
