using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts.Constants;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.Contracts.Standards.ERC20.TokenList;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.Contracts.Standards.ERC20
{
    public class ERC20Service
    {
        private readonly IEthApiContractService _ethApiContractService;

        public ERC20Service(IEthApiContractService ethApiContractService)
        {
            _ethApiContractService = ethApiContractService;
        }

        public ERC20ContractService GetERC20ContractService(string contractAddress)
        {
            return new ERC20ContractService(_ethApiContractService, contractAddress);
        }

#if !DOTNET35

        public async Task<List<TokenOwnerBalance>> GetAllTokenBalancesUsingMultiCallAsync(IEnumerable<string> ownerAddresses,
            IEnumerable<string> contractAddresses, BlockParameter block,
            int numberOfCallsPerRequest = MultiQueryHandler.DEFAULT_CALLS_PER_REQUEST,
            string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
        {
            var balanceCalls = new List<MulticallInputOutput<BalanceOfFunction, BalanceOfOutputDTO>>();
            foreach (var ownerAddress in ownerAddresses)
            {
                foreach (var contractAddress in contractAddresses)
                {
                    var balanceCall = new BalanceOfFunction() {Owner = ownerAddress};
                    balanceCalls.Add(new MulticallInputOutput<BalanceOfFunction, BalanceOfOutputDTO>(balanceCall,
                        contractAddress));
                }

            }

            var multiqueryHandler = this._ethApiContractService.GetMultiQueryHandler(multiCallAddress);
            var results = await multiqueryHandler.MultiCallAsync(numberOfCallsPerRequest, balanceCalls.ToArray());
            return balanceCalls.Select(x => new TokenOwnerBalance()
            {
                Balance = x.Output.Balance,
                ContractAddress = x.Target,
                Owner = x.Input.Owner,
            }).ToList();
        }

        public Task<List<TokenOwnerBalance>> GetAllTokenBalancesUsingMultiCallAsync(IEnumerable<string> ownerAddresses,
            IEnumerable<string> contractAddresses, int numberOfCallsPerRequest = MultiQueryHandler.DEFAULT_CALLS_PER_REQUEST,
            string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
        {
            return GetAllTokenBalancesUsingMultiCallAsync(ownerAddresses, contractAddresses,
                BlockParameter.CreateLatest(), numberOfCallsPerRequest, multiCallAddress);
        }

        public Task<List<TokenOwnerBalance>> GetAllTokenBalancesUsingMultiCallAsync(string ownerAddress,
            IEnumerable<string> contractAddresses, int numberOfCallsPerRequest = MultiQueryHandler.DEFAULT_CALLS_PER_REQUEST,
            string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
        {
            return GetAllTokenBalancesUsingMultiCallAsync(new string[] {ownerAddress}, contractAddresses,
                BlockParameter.CreateLatest(), numberOfCallsPerRequest, multiCallAddress);
        }


        public async Task<List<TokenOwnerInfo>> GetAllTokenBalancesUsingMultiCallAsync(IEnumerable<string> ownerAddresses,
            IEnumerable<Token> tokens, BlockParameter block,
            int numberOfCallsPerRequest = MultiQueryHandler.DEFAULT_CALLS_PER_REQUEST,
        string multiCallAddress = CommonAddresses.MULTICALL_ADDRESS)
        {
            var tokenBalances = await GetAllTokenBalancesUsingMultiCallAsync(ownerAddresses, tokens.Select(x => x.Address),
                block, numberOfCallsPerRequest, multiCallAddress);
            var returnInfo = new List<TokenOwnerInfo>();
            foreach (var token in tokens)
            {
                var ownerBalances = tokenBalances.Where(x => x.ContractAddress.IsTheSameAddress(token.Address));
                var tokenOwnerBalances = ownerBalances as TokenOwnerBalance[] ?? ownerBalances.ToArray();
                if (tokenOwnerBalances.Any())
                {
                    returnInfo.Add(new TokenOwnerInfo(){Token = token, OwnersBalances = tokenOwnerBalances.ToList()});
                }
            }

            return returnInfo;
        }
#endif

    }

    public class TokenOwnerInfo
    {
        public Token Token { get; set; }

        public BigInteger GetTotalBalance()
        { 
            BigInteger balance = 0;
            foreach (var tokenOwnerBalance in OwnersBalances)
            {
                balance += tokenOwnerBalance.Balance;
            }
            return balance;
        }

        public List<TokenOwnerBalance> OwnersBalances { get; set; } = new List<TokenOwnerBalance>();
        public List<TokenExchangeRate> TokenExchangeRate { get; set; } = new List<TokenExchangeRate>();
    }

    public class TokenExchangeRate
    {
        public string Currency { get; set; }
        public decimal Price { get; set; }
    }
}