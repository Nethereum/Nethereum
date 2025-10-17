using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.Util;
using Nethereum.Web3;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Uniswap.Accounts
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

    public class AccountApprovalService
    {
        public IWeb3 Web3 { get; }

        public AccountApprovalService(IWeb3 web3)
        {
            Web3 = web3;
        }

        public async Task<TokenApprovalStatus> CheckApprovalAsync(
           
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

            var erc20 = Web3.Eth.ERC20.GetContractService(tokenAddress);
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

        public async Task<string> ApproveAsync(
            string tokenAddress,
            string spender,
            BigInteger amount)
        {
            if (AddressUtil.Current.IsAnEmptyAddress(tokenAddress) ||
                tokenAddress.Equals(AddressUtil.ZERO_ADDRESS, System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var erc20 = Web3.Eth.ERC20.GetContractService(tokenAddress);
            return await erc20.ApproveRequestAsync(spender, amount);
        }

        public async Task<TokenApprovalStatus> CheckAndApproveIfNeededAsync(
            
            string tokenAddress,
            string owner,
            string spender,
            BigInteger requiredAmount)
        {
            var status = await CheckApprovalAsync(tokenAddress, owner, spender, requiredAmount);

            if (!status.IsApproved)
            {
                await ApproveAsync(tokenAddress, spender, requiredAmount);
                status = await CheckApprovalAsync(tokenAddress, owner, spender, requiredAmount);
            }

            return status;
        }

        public static BigInteger GetMaxApprovalAmount()
        {
            return Nethereum.ABI.IntType.GetMaxSignedValue(256);
        }
    }
}
