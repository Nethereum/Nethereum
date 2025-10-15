using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.ContractHandlers;
using System.Threading;
using Nethereum.Uniswap.V2.UniswapV2Router01.ContractDefinition;

namespace Nethereum.Uniswap.V2.UniswapV2Router01
{
    public partial class UniswapV2Router01Service
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, UniswapV2Router01Deployment uniswapV2Router01Deployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<UniswapV2Router01Deployment>().SendRequestAndWaitForReceiptAsync(uniswapV2Router01Deployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Web3.Web3 web3, UniswapV2Router01Deployment uniswapV2Router01Deployment)
        {
            return web3.Eth.GetContractDeploymentHandler<UniswapV2Router01Deployment>().SendRequestAsync(uniswapV2Router01Deployment);
        }

        public static async Task<UniswapV2Router01Service> DeployContractAndGetServiceAsync(Web3.Web3 web3, UniswapV2Router01Deployment uniswapV2Router01Deployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, uniswapV2Router01Deployment, cancellationTokenSource);
            return new UniswapV2Router01Service(web3, receipt.ContractAddress);
        }

        protected Web3.Web3 Web3{ get; }

        public ContractHandler ContractHandler { get; }

        public UniswapV2Router01Service(Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<string> WETHQueryAsync(WETHFunction wETHFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<WETHFunction, string>(wETHFunction, blockParameter);
        }

        
        public Task<string> WETHQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<WETHFunction, string>(null, blockParameter);
        }

        public Task<string> AddLiquidityRequestAsync(AddLiquidityFunction addLiquidityFunction)
        {
             return ContractHandler.SendRequestAsync(addLiquidityFunction);
        }

        public Task<TransactionReceipt> AddLiquidityRequestAndWaitForReceiptAsync(AddLiquidityFunction addLiquidityFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addLiquidityFunction, cancellationToken);
        }

        public Task<string> AddLiquidityRequestAsync(string tokenA, string tokenB, BigInteger amountADesired, BigInteger amountBDesired, BigInteger amountAMin, BigInteger amountBMin, string to, BigInteger deadline)
        {
            var addLiquidityFunction = new AddLiquidityFunction();
                addLiquidityFunction.TokenA = tokenA;
                addLiquidityFunction.TokenB = tokenB;
                addLiquidityFunction.AmountADesired = amountADesired;
                addLiquidityFunction.AmountBDesired = amountBDesired;
                addLiquidityFunction.AmountAMin = amountAMin;
                addLiquidityFunction.AmountBMin = amountBMin;
                addLiquidityFunction.To = to;
                addLiquidityFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAsync(addLiquidityFunction);
        }

        public Task<TransactionReceipt> AddLiquidityRequestAndWaitForReceiptAsync(string tokenA, string tokenB, BigInteger amountADesired, BigInteger amountBDesired, BigInteger amountAMin, BigInteger amountBMin, string to, BigInteger deadline, CancellationTokenSource cancellationToken = null)
        {
            var addLiquidityFunction = new AddLiquidityFunction();
                addLiquidityFunction.TokenA = tokenA;
                addLiquidityFunction.TokenB = tokenB;
                addLiquidityFunction.AmountADesired = amountADesired;
                addLiquidityFunction.AmountBDesired = amountBDesired;
                addLiquidityFunction.AmountAMin = amountAMin;
                addLiquidityFunction.AmountBMin = amountBMin;
                addLiquidityFunction.To = to;
                addLiquidityFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addLiquidityFunction, cancellationToken);
        }

        public Task<string> AddLiquidityETHRequestAsync(AddLiquidityETHFunction addLiquidityETHFunction)
        {
             return ContractHandler.SendRequestAsync(addLiquidityETHFunction);
        }

        public Task<TransactionReceipt> AddLiquidityETHRequestAndWaitForReceiptAsync(AddLiquidityETHFunction addLiquidityETHFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addLiquidityETHFunction, cancellationToken);
        }

        public Task<string> AddLiquidityETHRequestAsync(string token, BigInteger amountTokenDesired, BigInteger amountTokenMin, BigInteger amountETHMin, string to, BigInteger deadline)
        {
            var addLiquidityETHFunction = new AddLiquidityETHFunction();
                addLiquidityETHFunction.Token = token;
                addLiquidityETHFunction.AmountTokenDesired = amountTokenDesired;
                addLiquidityETHFunction.AmountTokenMin = amountTokenMin;
                addLiquidityETHFunction.AmountETHMin = amountETHMin;
                addLiquidityETHFunction.To = to;
                addLiquidityETHFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAsync(addLiquidityETHFunction);
        }

        public Task<TransactionReceipt> AddLiquidityETHRequestAndWaitForReceiptAsync(string token, BigInteger amountTokenDesired, BigInteger amountTokenMin, BigInteger amountETHMin, string to, BigInteger deadline, CancellationTokenSource cancellationToken = null)
        {
            var addLiquidityETHFunction = new AddLiquidityETHFunction();
                addLiquidityETHFunction.Token = token;
                addLiquidityETHFunction.AmountTokenDesired = amountTokenDesired;
                addLiquidityETHFunction.AmountTokenMin = amountTokenMin;
                addLiquidityETHFunction.AmountETHMin = amountETHMin;
                addLiquidityETHFunction.To = to;
                addLiquidityETHFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(addLiquidityETHFunction, cancellationToken);
        }

        public Task<string> FactoryQueryAsync(FactoryFunction factoryFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FactoryFunction, string>(factoryFunction, blockParameter);
        }

        
        public Task<string> FactoryQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FactoryFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> GetAmountInQueryAsync(GetAmountInFunction getAmountInFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAmountInFunction, BigInteger>(getAmountInFunction, blockParameter);
        }

        
        public Task<BigInteger> GetAmountInQueryAsync(BigInteger amountOut, BigInteger reserveIn, BigInteger reserveOut, BlockParameter blockParameter = null)
        {
            var getAmountInFunction = new GetAmountInFunction();
                getAmountInFunction.AmountOut = amountOut;
                getAmountInFunction.ReserveIn = reserveIn;
                getAmountInFunction.ReserveOut = reserveOut;
            
            return ContractHandler.QueryAsync<GetAmountInFunction, BigInteger>(getAmountInFunction, blockParameter);
        }

        public Task<BigInteger> GetAmountOutQueryAsync(GetAmountOutFunction getAmountOutFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAmountOutFunction, BigInteger>(getAmountOutFunction, blockParameter);
        }

        
        public Task<BigInteger> GetAmountOutQueryAsync(BigInteger amountIn, BigInteger reserveIn, BigInteger reserveOut, BlockParameter blockParameter = null)
        {
            var getAmountOutFunction = new GetAmountOutFunction();
                getAmountOutFunction.AmountIn = amountIn;
                getAmountOutFunction.ReserveIn = reserveIn;
                getAmountOutFunction.ReserveOut = reserveOut;
            
            return ContractHandler.QueryAsync<GetAmountOutFunction, BigInteger>(getAmountOutFunction, blockParameter);
        }

        public Task<List<BigInteger>> GetAmountsInQueryAsync(GetAmountsInFunction getAmountsInFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAmountsInFunction, List<BigInteger>>(getAmountsInFunction, blockParameter);
        }

        
        public Task<List<BigInteger>> GetAmountsInQueryAsync(BigInteger amountOut, List<string> path, BlockParameter blockParameter = null)
        {
            var getAmountsInFunction = new GetAmountsInFunction();
                getAmountsInFunction.AmountOut = amountOut;
                getAmountsInFunction.Path = path;
            
            return ContractHandler.QueryAsync<GetAmountsInFunction, List<BigInteger>>(getAmountsInFunction, blockParameter);
        }

        public Task<List<BigInteger>> GetAmountsOutQueryAsync(GetAmountsOutFunction getAmountsOutFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAmountsOutFunction, List<BigInteger>>(getAmountsOutFunction, blockParameter);
        }

        
        public Task<List<BigInteger>> GetAmountsOutQueryAsync(BigInteger amountIn, List<string> path, BlockParameter blockParameter = null)
        {
            var getAmountsOutFunction = new GetAmountsOutFunction();
                getAmountsOutFunction.AmountIn = amountIn;
                getAmountsOutFunction.Path = path;
            
            return ContractHandler.QueryAsync<GetAmountsOutFunction, List<BigInteger>>(getAmountsOutFunction, blockParameter);
        }

        public Task<BigInteger> QuoteQueryAsync(QuoteFunction quoteFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<QuoteFunction, BigInteger>(quoteFunction, blockParameter);
        }

        
        public Task<BigInteger> QuoteQueryAsync(BigInteger amountA, BigInteger reserveA, BigInteger reserveB, BlockParameter blockParameter = null)
        {
            var quoteFunction = new QuoteFunction();
                quoteFunction.AmountA = amountA;
                quoteFunction.ReserveA = reserveA;
                quoteFunction.ReserveB = reserveB;
            
            return ContractHandler.QueryAsync<QuoteFunction, BigInteger>(quoteFunction, blockParameter);
        }

        public Task<string> RemoveLiquidityRequestAsync(RemoveLiquidityFunction removeLiquidityFunction)
        {
             return ContractHandler.SendRequestAsync(removeLiquidityFunction);
        }

        public Task<TransactionReceipt> RemoveLiquidityRequestAndWaitForReceiptAsync(RemoveLiquidityFunction removeLiquidityFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeLiquidityFunction, cancellationToken);
        }

        public Task<string> RemoveLiquidityRequestAsync(string tokenA, string tokenB, BigInteger liquidity, BigInteger amountAMin, BigInteger amountBMin, string to, BigInteger deadline)
        {
            var removeLiquidityFunction = new RemoveLiquidityFunction();
                removeLiquidityFunction.TokenA = tokenA;
                removeLiquidityFunction.TokenB = tokenB;
                removeLiquidityFunction.Liquidity = liquidity;
                removeLiquidityFunction.AmountAMin = amountAMin;
                removeLiquidityFunction.AmountBMin = amountBMin;
                removeLiquidityFunction.To = to;
                removeLiquidityFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAsync(removeLiquidityFunction);
        }

        public Task<TransactionReceipt> RemoveLiquidityRequestAndWaitForReceiptAsync(string tokenA, string tokenB, BigInteger liquidity, BigInteger amountAMin, BigInteger amountBMin, string to, BigInteger deadline, CancellationTokenSource cancellationToken = null)
        {
            var removeLiquidityFunction = new RemoveLiquidityFunction();
                removeLiquidityFunction.TokenA = tokenA;
                removeLiquidityFunction.TokenB = tokenB;
                removeLiquidityFunction.Liquidity = liquidity;
                removeLiquidityFunction.AmountAMin = amountAMin;
                removeLiquidityFunction.AmountBMin = amountBMin;
                removeLiquidityFunction.To = to;
                removeLiquidityFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeLiquidityFunction, cancellationToken);
        }

        public Task<string> RemoveLiquidityETHRequestAsync(RemoveLiquidityETHFunction removeLiquidityETHFunction)
        {
             return ContractHandler.SendRequestAsync(removeLiquidityETHFunction);
        }

        public Task<TransactionReceipt> RemoveLiquidityETHRequestAndWaitForReceiptAsync(RemoveLiquidityETHFunction removeLiquidityETHFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeLiquidityETHFunction, cancellationToken);
        }

        public Task<string> RemoveLiquidityETHRequestAsync(string token, BigInteger liquidity, BigInteger amountTokenMin, BigInteger amountETHMin, string to, BigInteger deadline)
        {
            var removeLiquidityETHFunction = new RemoveLiquidityETHFunction();
                removeLiquidityETHFunction.Token = token;
                removeLiquidityETHFunction.Liquidity = liquidity;
                removeLiquidityETHFunction.AmountTokenMin = amountTokenMin;
                removeLiquidityETHFunction.AmountETHMin = amountETHMin;
                removeLiquidityETHFunction.To = to;
                removeLiquidityETHFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAsync(removeLiquidityETHFunction);
        }

        public Task<TransactionReceipt> RemoveLiquidityETHRequestAndWaitForReceiptAsync(string token, BigInteger liquidity, BigInteger amountTokenMin, BigInteger amountETHMin, string to, BigInteger deadline, CancellationTokenSource cancellationToken = null)
        {
            var removeLiquidityETHFunction = new RemoveLiquidityETHFunction();
                removeLiquidityETHFunction.Token = token;
                removeLiquidityETHFunction.Liquidity = liquidity;
                removeLiquidityETHFunction.AmountTokenMin = amountTokenMin;
                removeLiquidityETHFunction.AmountETHMin = amountETHMin;
                removeLiquidityETHFunction.To = to;
                removeLiquidityETHFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeLiquidityETHFunction, cancellationToken);
        }

        public Task<string> RemoveLiquidityETHWithPermitRequestAsync(RemoveLiquidityETHWithPermitFunction removeLiquidityETHWithPermitFunction)
        {
             return ContractHandler.SendRequestAsync(removeLiquidityETHWithPermitFunction);
        }

        public Task<TransactionReceipt> RemoveLiquidityETHWithPermitRequestAndWaitForReceiptAsync(RemoveLiquidityETHWithPermitFunction removeLiquidityETHWithPermitFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeLiquidityETHWithPermitFunction, cancellationToken);
        }

        public Task<string> RemoveLiquidityETHWithPermitRequestAsync(string token, BigInteger liquidity, BigInteger amountTokenMin, BigInteger amountETHMin, string to, BigInteger deadline, bool approveMax, byte v, byte[] r, byte[] s)
        {
            var removeLiquidityETHWithPermitFunction = new RemoveLiquidityETHWithPermitFunction();
                removeLiquidityETHWithPermitFunction.Token = token;
                removeLiquidityETHWithPermitFunction.Liquidity = liquidity;
                removeLiquidityETHWithPermitFunction.AmountTokenMin = amountTokenMin;
                removeLiquidityETHWithPermitFunction.AmountETHMin = amountETHMin;
                removeLiquidityETHWithPermitFunction.To = to;
                removeLiquidityETHWithPermitFunction.Deadline = deadline;
                removeLiquidityETHWithPermitFunction.ApproveMax = approveMax;
                removeLiquidityETHWithPermitFunction.V = v;
                removeLiquidityETHWithPermitFunction.R = r;
                removeLiquidityETHWithPermitFunction.S = s;
            
             return ContractHandler.SendRequestAsync(removeLiquidityETHWithPermitFunction);
        }

        public Task<TransactionReceipt> RemoveLiquidityETHWithPermitRequestAndWaitForReceiptAsync(string token, BigInteger liquidity, BigInteger amountTokenMin, BigInteger amountETHMin, string to, BigInteger deadline, bool approveMax, byte v, byte[] r, byte[] s, CancellationTokenSource cancellationToken = null)
        {
            var removeLiquidityETHWithPermitFunction = new RemoveLiquidityETHWithPermitFunction();
                removeLiquidityETHWithPermitFunction.Token = token;
                removeLiquidityETHWithPermitFunction.Liquidity = liquidity;
                removeLiquidityETHWithPermitFunction.AmountTokenMin = amountTokenMin;
                removeLiquidityETHWithPermitFunction.AmountETHMin = amountETHMin;
                removeLiquidityETHWithPermitFunction.To = to;
                removeLiquidityETHWithPermitFunction.Deadline = deadline;
                removeLiquidityETHWithPermitFunction.ApproveMax = approveMax;
                removeLiquidityETHWithPermitFunction.V = v;
                removeLiquidityETHWithPermitFunction.R = r;
                removeLiquidityETHWithPermitFunction.S = s;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeLiquidityETHWithPermitFunction, cancellationToken);
        }

        public Task<string> RemoveLiquidityWithPermitRequestAsync(RemoveLiquidityWithPermitFunction removeLiquidityWithPermitFunction)
        {
             return ContractHandler.SendRequestAsync(removeLiquidityWithPermitFunction);
        }

        public Task<TransactionReceipt> RemoveLiquidityWithPermitRequestAndWaitForReceiptAsync(RemoveLiquidityWithPermitFunction removeLiquidityWithPermitFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeLiquidityWithPermitFunction, cancellationToken);
        }

        public Task<string> RemoveLiquidityWithPermitRequestAsync(string tokenA, string tokenB, BigInteger liquidity, BigInteger amountAMin, BigInteger amountBMin, string to, BigInteger deadline, bool approveMax, byte v, byte[] r, byte[] s)
        {
            var removeLiquidityWithPermitFunction = new RemoveLiquidityWithPermitFunction();
                removeLiquidityWithPermitFunction.TokenA = tokenA;
                removeLiquidityWithPermitFunction.TokenB = tokenB;
                removeLiquidityWithPermitFunction.Liquidity = liquidity;
                removeLiquidityWithPermitFunction.AmountAMin = amountAMin;
                removeLiquidityWithPermitFunction.AmountBMin = amountBMin;
                removeLiquidityWithPermitFunction.To = to;
                removeLiquidityWithPermitFunction.Deadline = deadline;
                removeLiquidityWithPermitFunction.ApproveMax = approveMax;
                removeLiquidityWithPermitFunction.V = v;
                removeLiquidityWithPermitFunction.R = r;
                removeLiquidityWithPermitFunction.S = s;
            
             return ContractHandler.SendRequestAsync(removeLiquidityWithPermitFunction);
        }

        public Task<TransactionReceipt> RemoveLiquidityWithPermitRequestAndWaitForReceiptAsync(string tokenA, string tokenB, BigInteger liquidity, BigInteger amountAMin, BigInteger amountBMin, string to, BigInteger deadline, bool approveMax, byte v, byte[] r, byte[] s, CancellationTokenSource cancellationToken = null)
        {
            var removeLiquidityWithPermitFunction = new RemoveLiquidityWithPermitFunction();
                removeLiquidityWithPermitFunction.TokenA = tokenA;
                removeLiquidityWithPermitFunction.TokenB = tokenB;
                removeLiquidityWithPermitFunction.Liquidity = liquidity;
                removeLiquidityWithPermitFunction.AmountAMin = amountAMin;
                removeLiquidityWithPermitFunction.AmountBMin = amountBMin;
                removeLiquidityWithPermitFunction.To = to;
                removeLiquidityWithPermitFunction.Deadline = deadline;
                removeLiquidityWithPermitFunction.ApproveMax = approveMax;
                removeLiquidityWithPermitFunction.V = v;
                removeLiquidityWithPermitFunction.R = r;
                removeLiquidityWithPermitFunction.S = s;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removeLiquidityWithPermitFunction, cancellationToken);
        }

        public Task<string> SwapETHForExactTokensRequestAsync(SwapETHForExactTokensFunction swapETHForExactTokensFunction)
        {
             return ContractHandler.SendRequestAsync(swapETHForExactTokensFunction);
        }

        public Task<TransactionReceipt> SwapETHForExactTokensRequestAndWaitForReceiptAsync(SwapETHForExactTokensFunction swapETHForExactTokensFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapETHForExactTokensFunction, cancellationToken);
        }

        public Task<string> SwapETHForExactTokensRequestAsync(BigInteger amountOut, List<string> path, string to, BigInteger deadline)
        {
            var swapETHForExactTokensFunction = new SwapETHForExactTokensFunction();
                swapETHForExactTokensFunction.AmountOut = amountOut;
                swapETHForExactTokensFunction.Path = path;
                swapETHForExactTokensFunction.To = to;
                swapETHForExactTokensFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAsync(swapETHForExactTokensFunction);
        }

        public Task<TransactionReceipt> SwapETHForExactTokensRequestAndWaitForReceiptAsync(BigInteger amountOut, List<string> path, string to, BigInteger deadline, CancellationTokenSource cancellationToken = null)
        {
            var swapETHForExactTokensFunction = new SwapETHForExactTokensFunction();
                swapETHForExactTokensFunction.AmountOut = amountOut;
                swapETHForExactTokensFunction.Path = path;
                swapETHForExactTokensFunction.To = to;
                swapETHForExactTokensFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapETHForExactTokensFunction, cancellationToken);
        }

        public Task<string> SwapExactETHForTokensRequestAsync(SwapExactETHForTokensFunction swapExactETHForTokensFunction)
        {
             return ContractHandler.SendRequestAsync(swapExactETHForTokensFunction);
        }

        public Task<TransactionReceipt> SwapExactETHForTokensRequestAndWaitForReceiptAsync(SwapExactETHForTokensFunction swapExactETHForTokensFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapExactETHForTokensFunction, cancellationToken);
        }

        public Task<string> SwapExactETHForTokensRequestAsync(BigInteger amountOutMin, List<string> path, string to, BigInteger deadline)
        {
            var swapExactETHForTokensFunction = new SwapExactETHForTokensFunction();
                swapExactETHForTokensFunction.AmountOutMin = amountOutMin;
                swapExactETHForTokensFunction.Path = path;
                swapExactETHForTokensFunction.To = to;
                swapExactETHForTokensFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAsync(swapExactETHForTokensFunction);
        }

        public Task<TransactionReceipt> SwapExactETHForTokensRequestAndWaitForReceiptAsync(BigInteger amountOutMin, List<string> path, string to, BigInteger deadline, CancellationTokenSource cancellationToken = null)
        {
            var swapExactETHForTokensFunction = new SwapExactETHForTokensFunction();
                swapExactETHForTokensFunction.AmountOutMin = amountOutMin;
                swapExactETHForTokensFunction.Path = path;
                swapExactETHForTokensFunction.To = to;
                swapExactETHForTokensFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapExactETHForTokensFunction, cancellationToken);
        }

        public Task<string> SwapExactTokensForETHRequestAsync(SwapExactTokensForETHFunction swapExactTokensForETHFunction)
        {
             return ContractHandler.SendRequestAsync(swapExactTokensForETHFunction);
        }

        public Task<TransactionReceipt> SwapExactTokensForETHRequestAndWaitForReceiptAsync(SwapExactTokensForETHFunction swapExactTokensForETHFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapExactTokensForETHFunction, cancellationToken);
        }

        public Task<string> SwapExactTokensForETHRequestAsync(BigInteger amountIn, BigInteger amountOutMin, List<string> path, string to, BigInteger deadline)
        {
            var swapExactTokensForETHFunction = new SwapExactTokensForETHFunction();
                swapExactTokensForETHFunction.AmountIn = amountIn;
                swapExactTokensForETHFunction.AmountOutMin = amountOutMin;
                swapExactTokensForETHFunction.Path = path;
                swapExactTokensForETHFunction.To = to;
                swapExactTokensForETHFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAsync(swapExactTokensForETHFunction);
        }

        public Task<TransactionReceipt> SwapExactTokensForETHRequestAndWaitForReceiptAsync(BigInteger amountIn, BigInteger amountOutMin, List<string> path, string to, BigInteger deadline, CancellationTokenSource cancellationToken = null)
        {
            var swapExactTokensForETHFunction = new SwapExactTokensForETHFunction();
                swapExactTokensForETHFunction.AmountIn = amountIn;
                swapExactTokensForETHFunction.AmountOutMin = amountOutMin;
                swapExactTokensForETHFunction.Path = path;
                swapExactTokensForETHFunction.To = to;
                swapExactTokensForETHFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapExactTokensForETHFunction, cancellationToken);
        }

        public Task<string> SwapExactTokensForTokensRequestAsync(SwapExactTokensForTokensFunction swapExactTokensForTokensFunction)
        {
             return ContractHandler.SendRequestAsync(swapExactTokensForTokensFunction);
        }

        public Task<TransactionReceipt> SwapExactTokensForTokensRequestAndWaitForReceiptAsync(SwapExactTokensForTokensFunction swapExactTokensForTokensFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapExactTokensForTokensFunction, cancellationToken);
        }

        public Task<string> SwapExactTokensForTokensRequestAsync(BigInteger amountIn, BigInteger amountOutMin, List<string> path, string to, BigInteger deadline)
        {
            var swapExactTokensForTokensFunction = new SwapExactTokensForTokensFunction();
                swapExactTokensForTokensFunction.AmountIn = amountIn;
                swapExactTokensForTokensFunction.AmountOutMin = amountOutMin;
                swapExactTokensForTokensFunction.Path = path;
                swapExactTokensForTokensFunction.To = to;
                swapExactTokensForTokensFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAsync(swapExactTokensForTokensFunction);
        }

        public Task<TransactionReceipt> SwapExactTokensForTokensRequestAndWaitForReceiptAsync(BigInteger amountIn, BigInteger amountOutMin, List<string> path, string to, BigInteger deadline, CancellationTokenSource cancellationToken = null)
        {
            var swapExactTokensForTokensFunction = new SwapExactTokensForTokensFunction();
                swapExactTokensForTokensFunction.AmountIn = amountIn;
                swapExactTokensForTokensFunction.AmountOutMin = amountOutMin;
                swapExactTokensForTokensFunction.Path = path;
                swapExactTokensForTokensFunction.To = to;
                swapExactTokensForTokensFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapExactTokensForTokensFunction, cancellationToken);
        }

        public Task<string> SwapTokensForExactETHRequestAsync(SwapTokensForExactETHFunction swapTokensForExactETHFunction)
        {
             return ContractHandler.SendRequestAsync(swapTokensForExactETHFunction);
        }

        public Task<TransactionReceipt> SwapTokensForExactETHRequestAndWaitForReceiptAsync(SwapTokensForExactETHFunction swapTokensForExactETHFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapTokensForExactETHFunction, cancellationToken);
        }

        public Task<string> SwapTokensForExactETHRequestAsync(BigInteger amountOut, BigInteger amountInMax, List<string> path, string to, BigInteger deadline)
        {
            var swapTokensForExactETHFunction = new SwapTokensForExactETHFunction();
                swapTokensForExactETHFunction.AmountOut = amountOut;
                swapTokensForExactETHFunction.AmountInMax = amountInMax;
                swapTokensForExactETHFunction.Path = path;
                swapTokensForExactETHFunction.To = to;
                swapTokensForExactETHFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAsync(swapTokensForExactETHFunction);
        }

        public Task<TransactionReceipt> SwapTokensForExactETHRequestAndWaitForReceiptAsync(BigInteger amountOut, BigInteger amountInMax, List<string> path, string to, BigInteger deadline, CancellationTokenSource cancellationToken = null)
        {
            var swapTokensForExactETHFunction = new SwapTokensForExactETHFunction();
                swapTokensForExactETHFunction.AmountOut = amountOut;
                swapTokensForExactETHFunction.AmountInMax = amountInMax;
                swapTokensForExactETHFunction.Path = path;
                swapTokensForExactETHFunction.To = to;
                swapTokensForExactETHFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapTokensForExactETHFunction, cancellationToken);
        }

        public Task<string> SwapTokensForExactTokensRequestAsync(SwapTokensForExactTokensFunction swapTokensForExactTokensFunction)
        {
             return ContractHandler.SendRequestAsync(swapTokensForExactTokensFunction);
        }

        public Task<TransactionReceipt> SwapTokensForExactTokensRequestAndWaitForReceiptAsync(SwapTokensForExactTokensFunction swapTokensForExactTokensFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapTokensForExactTokensFunction, cancellationToken);
        }

        public Task<string> SwapTokensForExactTokensRequestAsync(BigInteger amountOut, BigInteger amountInMax, List<string> path, string to, BigInteger deadline)
        {
            var swapTokensForExactTokensFunction = new SwapTokensForExactTokensFunction();
                swapTokensForExactTokensFunction.AmountOut = amountOut;
                swapTokensForExactTokensFunction.AmountInMax = amountInMax;
                swapTokensForExactTokensFunction.Path = path;
                swapTokensForExactTokensFunction.To = to;
                swapTokensForExactTokensFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAsync(swapTokensForExactTokensFunction);
        }

        public Task<TransactionReceipt> SwapTokensForExactTokensRequestAndWaitForReceiptAsync(BigInteger amountOut, BigInteger amountInMax, List<string> path, string to, BigInteger deadline, CancellationTokenSource cancellationToken = null)
        {
            var swapTokensForExactTokensFunction = new SwapTokensForExactTokensFunction();
                swapTokensForExactTokensFunction.AmountOut = amountOut;
                swapTokensForExactTokensFunction.AmountInMax = amountInMax;
                swapTokensForExactTokensFunction.Path = path;
                swapTokensForExactTokensFunction.To = to;
                swapTokensForExactTokensFunction.Deadline = deadline;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapTokensForExactTokensFunction, cancellationToken);
        }
    }
}
