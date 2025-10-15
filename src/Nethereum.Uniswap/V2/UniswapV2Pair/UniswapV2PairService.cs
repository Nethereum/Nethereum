using System.Threading.Tasks;
using System.Numerics;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.ContractHandlers;
using System.Threading;
using Nethereum.Uniswap.V2.UniswapV2Pair.ContractDefinition;

namespace Nethereum.Uniswap.V2.UniswapV2Pair
{
    public partial class UniswapV2PairService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, UniswapV2PairDeployment uniswapV2PairDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<UniswapV2PairDeployment>().SendRequestAndWaitForReceiptAsync(uniswapV2PairDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Web3.Web3 web3, UniswapV2PairDeployment uniswapV2PairDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<UniswapV2PairDeployment>().SendRequestAsync(uniswapV2PairDeployment);
        }

        public static async Task<UniswapV2PairService> DeployContractAndGetServiceAsync(Web3.Web3 web3, UniswapV2PairDeployment uniswapV2PairDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, uniswapV2PairDeployment, cancellationTokenSource);
            return new UniswapV2PairService(web3, receipt.ContractAddress);
        }

        protected Web3.Web3 Web3{ get; }

        public ContractHandler ContractHandler { get; }

        public UniswapV2PairService(Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<byte[]> DOMAIN_SEPARATORQueryAsync(DOMAIN_SEPARATORFunction dOMAIN_SEPARATORFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DOMAIN_SEPARATORFunction, byte[]>(dOMAIN_SEPARATORFunction, blockParameter);
        }

        
        public Task<byte[]> DOMAIN_SEPARATORQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DOMAIN_SEPARATORFunction, byte[]>(null, blockParameter);
        }

        public Task<BigInteger> MINIMUM_LIQUIDITYQueryAsync(MINIMUM_LIQUIDITYFunction mINIMUM_LIQUIDITYFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MINIMUM_LIQUIDITYFunction, BigInteger>(mINIMUM_LIQUIDITYFunction, blockParameter);
        }

        
        public Task<BigInteger> MINIMUM_LIQUIDITYQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MINIMUM_LIQUIDITYFunction, BigInteger>(null, blockParameter);
        }

        public Task<byte[]> PERMIT_TYPEHASHQueryAsync(PERMIT_TYPEHASHFunction pERMIT_TYPEHASHFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PERMIT_TYPEHASHFunction, byte[]>(pERMIT_TYPEHASHFunction, blockParameter);
        }

        
        public Task<byte[]> PERMIT_TYPEHASHQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PERMIT_TYPEHASHFunction, byte[]>(null, blockParameter);
        }

        public Task<BigInteger> AllowanceQueryAsync(AllowanceFunction allowanceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllowanceFunction, BigInteger>(allowanceFunction, blockParameter);
        }

        
        public Task<BigInteger> AllowanceQueryAsync(string owner, string spender, BlockParameter blockParameter = null)
        {
            var allowanceFunction = new AllowanceFunction();
                allowanceFunction.Owner = owner;
                allowanceFunction.Spender = spender;
            
            return ContractHandler.QueryAsync<AllowanceFunction, BigInteger>(allowanceFunction, blockParameter);
        }

        public Task<string> ApproveRequestAsync(ApproveFunction approveFunction)
        {
             return ContractHandler.SendRequestAsync(approveFunction);
        }

        public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(ApproveFunction approveFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }

        public Task<string> ApproveRequestAsync(string spender, BigInteger value)
        {
            var approveFunction = new ApproveFunction();
                approveFunction.Spender = spender;
                approveFunction.Value = value;
            
             return ContractHandler.SendRequestAsync(approveFunction);
        }

        public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(string spender, BigInteger value, CancellationTokenSource cancellationToken = null)
        {
            var approveFunction = new ApproveFunction();
                approveFunction.Spender = spender;
                approveFunction.Value = value;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }

        public Task<BigInteger> BalanceOfQueryAsync(BalanceOfFunction balanceOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        
        public Task<BigInteger> BalanceOfQueryAsync(string owner, BlockParameter blockParameter = null)
        {
            var balanceOfFunction = new BalanceOfFunction();
                balanceOfFunction.Owner = owner;
            
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        public Task<string> BurnRequestAsync(BurnFunction burnFunction)
        {
             return ContractHandler.SendRequestAsync(burnFunction);
        }

        public Task<TransactionReceipt> BurnRequestAndWaitForReceiptAsync(BurnFunction burnFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(burnFunction, cancellationToken);
        }

        public Task<string> BurnRequestAsync(string to)
        {
            var burnFunction = new BurnFunction();
                burnFunction.To = to;
            
             return ContractHandler.SendRequestAsync(burnFunction);
        }

        public Task<TransactionReceipt> BurnRequestAndWaitForReceiptAsync(string to, CancellationTokenSource cancellationToken = null)
        {
            var burnFunction = new BurnFunction();
                burnFunction.To = to;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(burnFunction, cancellationToken);
        }

        public Task<byte> DecimalsQueryAsync(DecimalsFunction decimalsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DecimalsFunction, byte>(decimalsFunction, blockParameter);
        }

        
        public Task<byte> DecimalsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DecimalsFunction, byte>(null, blockParameter);
        }

        public Task<string> FactoryQueryAsync(FactoryFunction factoryFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FactoryFunction, string>(factoryFunction, blockParameter);
        }

        
        public Task<string> FactoryQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<FactoryFunction, string>(null, blockParameter);
        }

        public Task<GetReservesOutputDTO> GetReservesQueryAsync(GetReservesFunction getReservesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetReservesFunction, GetReservesOutputDTO>(getReservesFunction, blockParameter);
        }

        public Task<GetReservesOutputDTO> GetReservesQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetReservesFunction, GetReservesOutputDTO>(null, blockParameter);
        }

        public Task<BigInteger> KLastQueryAsync(KLastFunction kLastFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<KLastFunction, BigInteger>(kLastFunction, blockParameter);
        }

        
        public Task<BigInteger> KLastQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<KLastFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> MintRequestAsync(MintFunction mintFunction)
        {
             return ContractHandler.SendRequestAsync(mintFunction);
        }

        public Task<TransactionReceipt> MintRequestAndWaitForReceiptAsync(MintFunction mintFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(mintFunction, cancellationToken);
        }

        public Task<string> MintRequestAsync(string to)
        {
            var mintFunction = new MintFunction();
                mintFunction.To = to;
            
             return ContractHandler.SendRequestAsync(mintFunction);
        }

        public Task<TransactionReceipt> MintRequestAndWaitForReceiptAsync(string to, CancellationTokenSource cancellationToken = null)
        {
            var mintFunction = new MintFunction();
                mintFunction.To = to;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(mintFunction, cancellationToken);
        }

        public Task<string> NameQueryAsync(NameFunction nameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
        }

        
        public Task<string> NameQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> NoncesQueryAsync(NoncesFunction noncesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NoncesFunction, BigInteger>(noncesFunction, blockParameter);
        }

        
        public Task<BigInteger> NoncesQueryAsync(string owner, BlockParameter blockParameter = null)
        {
            var noncesFunction = new NoncesFunction();
                noncesFunction.Owner = owner;
            
            return ContractHandler.QueryAsync<NoncesFunction, BigInteger>(noncesFunction, blockParameter);
        }

        public Task<string> PermitRequestAsync(PermitFunction permitFunction)
        {
             return ContractHandler.SendRequestAsync(permitFunction);
        }

        public Task<TransactionReceipt> PermitRequestAndWaitForReceiptAsync(PermitFunction permitFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitFunction, cancellationToken);
        }

        public Task<string> PermitRequestAsync(string owner, string spender, BigInteger value, BigInteger deadline, byte v, byte[] r, byte[] s)
        {
            var permitFunction = new PermitFunction();
                permitFunction.Owner = owner;
                permitFunction.Spender = spender;
                permitFunction.Value = value;
                permitFunction.Deadline = deadline;
                permitFunction.V = v;
                permitFunction.R = r;
                permitFunction.S = s;
            
             return ContractHandler.SendRequestAsync(permitFunction);
        }

        public Task<TransactionReceipt> PermitRequestAndWaitForReceiptAsync(string owner, string spender, BigInteger value, BigInteger deadline, byte v, byte[] r, byte[] s, CancellationTokenSource cancellationToken = null)
        {
            var permitFunction = new PermitFunction();
                permitFunction.Owner = owner;
                permitFunction.Spender = spender;
                permitFunction.Value = value;
                permitFunction.Deadline = deadline;
                permitFunction.V = v;
                permitFunction.R = r;
                permitFunction.S = s;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitFunction, cancellationToken);
        }

        public Task<BigInteger> Price0CumulativeLastQueryAsync(Price0CumulativeLastFunction price0CumulativeLastFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Price0CumulativeLastFunction, BigInteger>(price0CumulativeLastFunction, blockParameter);
        }

        
        public Task<BigInteger> Price0CumulativeLastQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Price0CumulativeLastFunction, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> Price1CumulativeLastQueryAsync(Price1CumulativeLastFunction price1CumulativeLastFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Price1CumulativeLastFunction, BigInteger>(price1CumulativeLastFunction, blockParameter);
        }

        
        public Task<BigInteger> Price1CumulativeLastQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Price1CumulativeLastFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> SkimRequestAsync(SkimFunction skimFunction)
        {
             return ContractHandler.SendRequestAsync(skimFunction);
        }

        public Task<TransactionReceipt> SkimRequestAndWaitForReceiptAsync(SkimFunction skimFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(skimFunction, cancellationToken);
        }

        public Task<string> SkimRequestAsync(string to)
        {
            var skimFunction = new SkimFunction();
                skimFunction.To = to;
            
             return ContractHandler.SendRequestAsync(skimFunction);
        }

        public Task<TransactionReceipt> SkimRequestAndWaitForReceiptAsync(string to, CancellationTokenSource cancellationToken = null)
        {
            var skimFunction = new SkimFunction();
                skimFunction.To = to;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(skimFunction, cancellationToken);
        }

        public Task<string> SwapRequestAsync(SwapFunction swapFunction)
        {
             return ContractHandler.SendRequestAsync(swapFunction);
        }

        public Task<TransactionReceipt> SwapRequestAndWaitForReceiptAsync(SwapFunction swapFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapFunction, cancellationToken);
        }

        public Task<string> SwapRequestAsync(BigInteger amount0Out, BigInteger amount1Out, string to, byte[] data)
        {
            var swapFunction = new SwapFunction();
                swapFunction.Amount0Out = amount0Out;
                swapFunction.Amount1Out = amount1Out;
                swapFunction.To = to;
                swapFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(swapFunction);
        }

        public Task<TransactionReceipt> SwapRequestAndWaitForReceiptAsync(BigInteger amount0Out, BigInteger amount1Out, string to, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var swapFunction = new SwapFunction();
                swapFunction.Amount0Out = amount0Out;
                swapFunction.Amount1Out = amount1Out;
                swapFunction.To = to;
                swapFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(swapFunction, cancellationToken);
        }

        public Task<string> SymbolQueryAsync(SymbolFunction symbolFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(symbolFunction, blockParameter);
        }

        
        public Task<string> SymbolQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(null, blockParameter);
        }

        public Task<string> SyncRequestAsync(SyncFunction syncFunction)
        {
             return ContractHandler.SendRequestAsync(syncFunction);
        }

        public Task<string> SyncRequestAsync()
        {
             return ContractHandler.SendRequestAsync<SyncFunction>();
        }

        public Task<TransactionReceipt> SyncRequestAndWaitForReceiptAsync(SyncFunction syncFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(syncFunction, cancellationToken);
        }

        public Task<TransactionReceipt> SyncRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<SyncFunction>(null, cancellationToken);
        }

        public Task<string> Token0QueryAsync(Token0Function token0Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Token0Function, string>(token0Function, blockParameter);
        }

        
        public Task<string> Token0QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Token0Function, string>(null, blockParameter);
        }

        public Task<string> Token1QueryAsync(Token1Function token1Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Token1Function, string>(token1Function, blockParameter);
        }

        
        public Task<string> Token1QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<Token1Function, string>(null, blockParameter);
        }

        public Task<BigInteger> TotalSupplyQueryAsync(TotalSupplyFunction totalSupplyFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(totalSupplyFunction, blockParameter);
        }

        
        public Task<BigInteger> TotalSupplyQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> TransferRequestAsync(TransferFunction transferFunction)
        {
             return ContractHandler.SendRequestAsync(transferFunction);
        }

        public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(TransferFunction transferFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, cancellationToken);
        }

        public Task<string> TransferRequestAsync(string to, BigInteger value)
        {
            var transferFunction = new TransferFunction();
                transferFunction.To = to;
                transferFunction.Value = value;
            
             return ContractHandler.SendRequestAsync(transferFunction);
        }

        public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(string to, BigInteger value, CancellationTokenSource cancellationToken = null)
        {
            var transferFunction = new TransferFunction();
                transferFunction.To = to;
                transferFunction.Value = value;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, cancellationToken);
        }

        public Task<string> TransferFromRequestAsync(TransferFromFunction transferFromFunction)
        {
             return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(TransferFromFunction transferFromFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }

        public Task<string> TransferFromRequestAsync(string from, string to, BigInteger value)
        {
            var transferFromFunction = new TransferFromFunction();
                transferFromFunction.From = from;
                transferFromFunction.To = to;
                transferFromFunction.Value = value;
            
             return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger value, CancellationTokenSource cancellationToken = null)
        {
            var transferFromFunction = new TransferFromFunction();
                transferFromFunction.From = from;
                transferFromFunction.To = to;
                transferFromFunction.Value = value;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }
    }
}
