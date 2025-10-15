using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.Util;
using Nethereum.Web3;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Uniswap.V4
{
    public class TokenApprovalStatus
    {
        public bool IsApproved { get; set; }
        public BigInteger CurrentAllowance { get; set; }
        public BigInteger RequiredAmount { get; set; }
        public string Token { get; set; }
        public string Spender { get; set; }
        public string Owner { get; set; }
    }

    public static class V4TokenApprovalHelper
    {
        public static async Task<TokenApprovalStatus> CheckApprovalAsync(
            IWeb3 web3,
            string tokenAddress,
            string owner,
            string spender,
            BigInteger requiredAmount)
        {
            if (AddressUtil.Current.IsAnEmptyAddress(tokenAddress) ||
                tokenAddress.Equals(AddressUtil.ZERO_ADDRESS, System.StringComparison.OrdinalIgnoreCase))
            {
                return new TokenApprovalStatus
                {
                    IsApproved = true,
                    CurrentAllowance = BigInteger.Zero,
                    RequiredAmount = requiredAmount,
                    Token = tokenAddress,
                    Spender = spender,
                    Owner = owner
                };
            }

            var erc20 = web3.Eth.ERC20.GetContractService(tokenAddress);
            var allowance = await erc20.AllowanceQueryAsync(owner, spender);

            return new TokenApprovalStatus
            {
                IsApproved = allowance >= requiredAmount,
                CurrentAllowance = allowance,
                RequiredAmount = requiredAmount,
                Token = tokenAddress,
                Spender = spender,
                Owner = owner
            };
        }

        public static async Task<string> ApproveAsync(
            IWeb3 web3,
            string tokenAddress,
            string spender,
            BigInteger amount)
        {
            if (AddressUtil.Current.IsAnEmptyAddress(tokenAddress) ||
                tokenAddress.Equals(AddressUtil.ZERO_ADDRESS, System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var erc20 = web3.Eth.ERC20.GetContractService(tokenAddress);
            return await erc20.ApproveRequestAsync(spender, amount);
        }

        public static async Task<TokenApprovalStatus> CheckAndApproveIfNeededAsync(
            IWeb3 web3,
            string tokenAddress,
            string owner,
            string spender,
            BigInteger requiredAmount)
        {
            var status = await CheckApprovalAsync(web3, tokenAddress, owner, spender, requiredAmount);

            if (!status.IsApproved)
            {
                await ApproveAsync(web3, tokenAddress, spender, requiredAmount);
                status = await CheckApprovalAsync(web3, tokenAddress, owner, spender, requiredAmount);
            }

            return status;
        }

        public static BigInteger GetMaxApprovalAmount()
        {
            return BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935");
        }
    }
}
