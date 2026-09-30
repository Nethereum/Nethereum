using System.Numerics;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;

using Nethereum.Documentation;
namespace Nethereum.AccountAbstraction.Paymasters
{
    [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "IPaymasterManager - drive a paymaster contract: deposit, sponsor, withdraw")]
    public interface IPaymasterManager
    {
        string Address { get; }
        string EntryPointAddress { get; }

        Task<SponsorResult> SponsorUserOperationAsync(PackedUserOperation userOp, SponsorContext? context = null);
        Task<BigInteger> GetDepositAsync();
        Task<TransactionReceipt> DepositAsync(BigInteger amount);
        Task<TransactionReceipt> WithdrawToAsync(string to, BigInteger amount);
    }

    [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "IVerifyingPaymasterManager - a sponsor signs each operation off-chain")]
    public interface IVerifyingPaymasterManager : IPaymasterManager
    {
        Task<SponsorResult> SponsorWithSignatureAsync(PackedUserOperation userOp, ulong validUntil, ulong validAfter, EthECKey signerKey);
        Task<byte[]> GetHashAsync(PackedUserOperation userOp, ulong validUntil, ulong validAfter);
    }

    [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "IDepositPaymasterManager - each user pre-funds their own sponsorship balance")]
    public interface IDepositPaymasterManager : IPaymasterManager
    {
        Task<BigInteger> GetUserDepositAsync(string account);
        Task<TransactionReceipt> DepositForAsync(string account, BigInteger amount);
        Task<TransactionReceipt> WithdrawFromAsync(string account, BigInteger amount);
    }

    [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "ITokenPaymasterManager - gas paid in an ERC-20")]
    public interface ITokenPaymasterManager : IPaymasterManager
    {
        Task<string> GetTokenAddressAsync();
        Task<BigInteger> EstimateTokenCostAsync(BigInteger ethCost);
        Task<BigInteger> GetTokenBalanceAsync(string account);
    }
}
