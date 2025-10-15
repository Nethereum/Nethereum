using Nethereum.Util;
using Nethereum.Web3;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Uniswap.V4
{
    public class BalanceValidationResult
    {
        public bool HasSufficientBalance { get; set; }
        public BigInteger CurrentBalance { get; set; }
        public BigInteger RequiredAmount { get; set; }
        public BigInteger Deficit { get; set; }
        public string Token { get; set; }
        public string Owner { get; set; }
        public string Message { get; set; }
    }

    public static class V4BalanceValidator
    {
        public static async Task<BalanceValidationResult> ValidateBalanceAsync(
            IWeb3 web3,
            string tokenAddress,
            string owner,
            BigInteger requiredAmount)
        {
            BigInteger balance;

            if (AddressUtil.Current.IsAnEmptyAddress(tokenAddress) ||
                tokenAddress.Equals(AddressUtil.ZERO_ADDRESS, System.StringComparison.OrdinalIgnoreCase))
            {
                balance = await web3.Eth.GetBalance.SendRequestAsync(owner);
            }
            else
            {
                var erc20 = web3.Eth.ERC20.GetContractService(tokenAddress);
                balance = await erc20.BalanceOfQueryAsync(owner);
            }

            var hasSufficientBalance = balance >= requiredAmount;
            var deficit = hasSufficientBalance ? BigInteger.Zero : requiredAmount - balance;

            return new BalanceValidationResult
            {
                HasSufficientBalance = hasSufficientBalance,
                CurrentBalance = balance,
                RequiredAmount = requiredAmount,
                Deficit = deficit,
                Token = tokenAddress,
                Owner = owner,
                Message = hasSufficientBalance
                    ? "Sufficient balance available"
                    : $"Insufficient balance. Required: {requiredAmount}, Available: {balance}, Deficit: {deficit}"
            };
        }

        public static async Task<bool> ValidateBalancesForLiquidityAsync(
            IWeb3 web3,
            string token0,
            string token1,
            string owner,
            BigInteger amount0Required,
            BigInteger amount1Required)
        {
            var balance0Result = await ValidateBalanceAsync(web3, token0, owner, amount0Required);
            var balance1Result = await ValidateBalanceAsync(web3, token1, owner, amount1Required);

            return balance0Result.HasSufficientBalance && balance1Result.HasSufficientBalance;
        }
    }
}
