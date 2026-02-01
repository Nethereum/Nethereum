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
using Nethereum.AccountAbstraction.Contracts.Interfaces.ITokenPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.ITokenPaymaster
{
    public partial class ITokenPaymasterService: ITokenPaymasterServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, ITokenPaymasterDeployment iTokenPaymasterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ITokenPaymasterDeployment>().SendRequestAndWaitForReceiptAsync(iTokenPaymasterDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, ITokenPaymasterDeployment iTokenPaymasterDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ITokenPaymasterDeployment>().SendRequestAsync(iTokenPaymasterDeployment);
        }

        public static async Task<ITokenPaymasterService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, ITokenPaymasterDeployment iTokenPaymasterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iTokenPaymasterDeployment, cancellationTokenSource);
            return new ITokenPaymasterService(web3, receipt.ContractAddress);
        }

        public ITokenPaymasterService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class ITokenPaymasterServiceBase: ContractWeb3ServiceBase
    {

        public ITokenPaymasterServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> PostOpRequestAsync(PostOpFunction postOpFunction)
        {
             return ContractHandler.SendRequestAsync(postOpFunction);
        }

        public virtual Task<TransactionReceipt> PostOpRequestAndWaitForReceiptAsync(PostOpFunction postOpFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postOpFunction, cancellationToken);
        }

        public virtual Task<string> PostOpRequestAsync(byte mode, byte[] context, BigInteger actualGasCost, BigInteger actualUserOpFeePerGas)
        {
            var postOpFunction = new PostOpFunction();
                postOpFunction.Mode = mode;
                postOpFunction.Context = context;
                postOpFunction.ActualGasCost = actualGasCost;
                postOpFunction.ActualUserOpFeePerGas = actualUserOpFeePerGas;
            
             return ContractHandler.SendRequestAsync(postOpFunction);
        }

        public virtual Task<TransactionReceipt> PostOpRequestAndWaitForReceiptAsync(byte mode, byte[] context, BigInteger actualGasCost, BigInteger actualUserOpFeePerGas, CancellationTokenSource cancellationToken = null)
        {
            var postOpFunction = new PostOpFunction();
                postOpFunction.Mode = mode;
                postOpFunction.Context = context;
                postOpFunction.ActualGasCost = actualGasCost;
                postOpFunction.ActualUserOpFeePerGas = actualUserOpFeePerGas;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postOpFunction, cancellationToken);
        }

        public Task<BigInteger> PriceMarkupQueryAsync(PriceMarkupFunction priceMarkupFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PriceMarkupFunction, BigInteger>(priceMarkupFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> PriceMarkupQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PriceMarkupFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> PriceOracleQueryAsync(PriceOracleFunction priceOracleFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PriceOracleFunction, string>(priceOracleFunction, blockParameter);
        }

        
        public virtual Task<string> PriceOracleQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PriceOracleFunction, string>(null, blockParameter);
        }

        public virtual Task<string> SetPriceMarkupRequestAsync(SetPriceMarkupFunction setPriceMarkupFunction)
        {
             return ContractHandler.SendRequestAsync(setPriceMarkupFunction);
        }

        public virtual Task<TransactionReceipt> SetPriceMarkupRequestAndWaitForReceiptAsync(SetPriceMarkupFunction setPriceMarkupFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setPriceMarkupFunction, cancellationToken);
        }

        public virtual Task<string> SetPriceMarkupRequestAsync(BigInteger markup)
        {
            var setPriceMarkupFunction = new SetPriceMarkupFunction();
                setPriceMarkupFunction.Markup = markup;
            
             return ContractHandler.SendRequestAsync(setPriceMarkupFunction);
        }

        public virtual Task<TransactionReceipt> SetPriceMarkupRequestAndWaitForReceiptAsync(BigInteger markup, CancellationTokenSource cancellationToken = null)
        {
            var setPriceMarkupFunction = new SetPriceMarkupFunction();
                setPriceMarkupFunction.Markup = markup;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setPriceMarkupFunction, cancellationToken);
        }

        public Task<string> TokenQueryAsync(TokenFunction tokenFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TokenFunction, string>(tokenFunction, blockParameter);
        }

        
        public virtual Task<string> TokenQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TokenFunction, string>(null, blockParameter);
        }

        public virtual Task<string> ValidatePaymasterUserOpRequestAsync(ValidatePaymasterUserOpFunction validatePaymasterUserOpFunction)
        {
             return ContractHandler.SendRequestAsync(validatePaymasterUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidatePaymasterUserOpRequestAndWaitForReceiptAsync(ValidatePaymasterUserOpFunction validatePaymasterUserOpFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validatePaymasterUserOpFunction, cancellationToken);
        }

        public virtual Task<string> ValidatePaymasterUserOpRequestAsync(PackedUserOperation userOp, byte[] userOpHash, BigInteger maxCost)
        {
            var validatePaymasterUserOpFunction = new ValidatePaymasterUserOpFunction();
                validatePaymasterUserOpFunction.UserOp = userOp;
                validatePaymasterUserOpFunction.UserOpHash = userOpHash;
                validatePaymasterUserOpFunction.MaxCost = maxCost;
            
             return ContractHandler.SendRequestAsync(validatePaymasterUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidatePaymasterUserOpRequestAndWaitForReceiptAsync(PackedUserOperation userOp, byte[] userOpHash, BigInteger maxCost, CancellationTokenSource cancellationToken = null)
        {
            var validatePaymasterUserOpFunction = new ValidatePaymasterUserOpFunction();
                validatePaymasterUserOpFunction.UserOp = userOp;
                validatePaymasterUserOpFunction.UserOpHash = userOpHash;
                validatePaymasterUserOpFunction.MaxCost = maxCost;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validatePaymasterUserOpFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(PostOpFunction),
                typeof(PriceMarkupFunction),
                typeof(PriceOracleFunction),
                typeof(SetPriceMarkupFunction),
                typeof(TokenFunction),
                typeof(ValidatePaymasterUserOpFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(TokenPaymentEventDTO)
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
