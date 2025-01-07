using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts;
using System.Threading;
using MyProject.Contracts.Standard_Token.ContractDefinition;

namespace MyProject.Contracts.Standard_Token
{
    public partial class StandardTokenService: StandardTokenServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, StandardTokenDeployment standardTokenDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>().SendRequestAndWaitForReceiptAsync(standardTokenDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, StandardTokenDeployment standardTokenDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<StandardTokenDeployment>().SendRequestAsync(standardTokenDeployment);
        }

        public static async Task<StandardTokenService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, StandardTokenDeployment standardTokenDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, standardTokenDeployment, cancellationTokenSource);
            return new StandardTokenService(web3, receipt.ContractAddress);
        }

        public StandardTokenService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class StandardTokenServiceBase: ContractWeb3ServiceBase
    {

        public StandardTokenServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<BigInteger> AllowanceQueryAsync(AllowanceFunction allowanceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllowanceFunction, BigInteger>(allowanceFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> AllowanceQueryAsync(string owner, string spender, BlockParameter blockParameter = null)
        {
            var allowanceFunction = new AllowanceFunction();
                allowanceFunction.Owner = owner;
                allowanceFunction.Spender = spender;
            
            return ContractHandler.QueryAsync<AllowanceFunction, BigInteger>(allowanceFunction, blockParameter);
        }

        public Task<BigInteger> AllowedQueryAsync(AllowedFunction allowedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllowedFunction, BigInteger>(allowedFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> AllowedQueryAsync(string returnValue1, string returnValue2, BlockParameter blockParameter = null)
        {
            var allowedFunction = new AllowedFunction();
                allowedFunction.ReturnValue1 = returnValue1;
                allowedFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryAsync<AllowedFunction, BigInteger>(allowedFunction, blockParameter);
        }

        public virtual Task<string> ApproveRequestAsync(ApproveFunction approveFunction)
        {
             return ContractHandler.SendRequestAsync(approveFunction);
        }

        public virtual Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(ApproveFunction approveFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }

        public virtual Task<string> ApproveRequestAsync(string spender, BigInteger value)
        {
            var approveFunction = new ApproveFunction();
                approveFunction.Spender = spender;
                approveFunction.Value = value;
            
             return ContractHandler.SendRequestAsync(approveFunction);
        }

        public virtual Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(string spender, BigInteger value, CancellationTokenSource cancellationToken = null)
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

        
        public virtual Task<BigInteger> BalanceOfQueryAsync(string owner, BlockParameter blockParameter = null)
        {
            var balanceOfFunction = new BalanceOfFunction();
                balanceOfFunction.Owner = owner;
            
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        public Task<BigInteger> BalancesQueryAsync(BalancesFunction balancesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalancesFunction, BigInteger>(balancesFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> BalancesQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var balancesFunction = new BalancesFunction();
                balancesFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<BalancesFunction, BigInteger>(balancesFunction, blockParameter);
        }

        public Task<byte> DecimalsQueryAsync(DecimalsFunction decimalsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DecimalsFunction, byte>(decimalsFunction, blockParameter);
        }

        
        public virtual Task<byte> DecimalsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DecimalsFunction, byte>(null, blockParameter);
        }

        public Task<string> NameQueryAsync(NameFunction nameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
        }

        
        public virtual Task<string> NameQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(null, blockParameter);
        }

        public Task<string> SymbolQueryAsync(SymbolFunction symbolFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(symbolFunction, blockParameter);
        }

        
        public virtual Task<string> SymbolQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> TotalSupplyQueryAsync(TotalSupplyFunction totalSupplyFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(totalSupplyFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> TotalSupplyQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(null, blockParameter);
        }

        public virtual Task<string> TransferRequestAsync(TransferFunction transferFunction)
        {
             return ContractHandler.SendRequestAsync(transferFunction);
        }

        public virtual Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(TransferFunction transferFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, cancellationToken);
        }

        public virtual Task<string> TransferRequestAsync(string to, BigInteger value)
        {
            var transferFunction = new TransferFunction();
                transferFunction.To = to;
                transferFunction.Value = value;
            
             return ContractHandler.SendRequestAsync(transferFunction);
        }

        public virtual Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(string to, BigInteger value, CancellationTokenSource cancellationToken = null)
        {
            var transferFunction = new TransferFunction();
                transferFunction.To = to;
                transferFunction.Value = value;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, cancellationToken);
        }

        public virtual Task<string> TransferFromRequestAsync(TransferFromFunction transferFromFunction)
        {
             return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public virtual Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(TransferFromFunction transferFromFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }

        public virtual Task<string> TransferFromRequestAsync(string from, string to, BigInteger value)
        {
            var transferFromFunction = new TransferFromFunction();
                transferFromFunction.From = from;
                transferFromFunction.To = to;
                transferFromFunction.Value = value;
            
             return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public virtual Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger value, CancellationTokenSource cancellationToken = null)
        {
            var transferFromFunction = new TransferFromFunction();
                transferFromFunction.From = from;
                transferFromFunction.To = to;
                transferFromFunction.Value = value;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AllowanceFunction),
                typeof(AllowedFunction),
                typeof(ApproveFunction),
                typeof(BalanceOfFunction),
                typeof(BalancesFunction),
                typeof(DecimalsFunction),
                typeof(NameFunction),
                typeof(SymbolFunction),
                typeof(TotalSupplyFunction),
                typeof(TransferFunction),
                typeof(TransferFromFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(ApprovalEventDTO),
                typeof(TransferEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {

            };
        }
    }
}
