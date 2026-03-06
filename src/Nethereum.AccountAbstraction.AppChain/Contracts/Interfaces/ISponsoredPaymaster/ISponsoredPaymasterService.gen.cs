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
using Nethereum.AccountAbstraction.AppChain.Contracts.Interfaces.ISponsoredPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.AppChain.Contracts.Interfaces.ISponsoredPaymaster
{
    public partial class ISponsoredPaymasterService: ISponsoredPaymasterServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, ISponsoredPaymasterDeployment iSponsoredPaymasterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ISponsoredPaymasterDeployment>().SendRequestAndWaitForReceiptAsync(iSponsoredPaymasterDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, ISponsoredPaymasterDeployment iSponsoredPaymasterDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ISponsoredPaymasterDeployment>().SendRequestAsync(iSponsoredPaymasterDeployment);
        }

        public static async Task<ISponsoredPaymasterService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, ISponsoredPaymasterDeployment iSponsoredPaymasterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iSponsoredPaymasterDeployment, cancellationTokenSource);
            return new ISponsoredPaymasterService(web3, receipt.ContractAddress);
        }

        public ISponsoredPaymasterService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class ISponsoredPaymasterServiceBase: ContractWeb3ServiceBase
    {

        public ISponsoredPaymasterServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<BigInteger> DailySponsoredQueryAsync(DailySponsoredFunction dailySponsoredFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DailySponsoredFunction, BigInteger>(dailySponsoredFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> DailySponsoredQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var dailySponsoredFunction = new DailySponsoredFunction();
                dailySponsoredFunction.Account = account;
            
            return ContractHandler.QueryAsync<DailySponsoredFunction, BigInteger>(dailySponsoredFunction, blockParameter);
        }

        public Task<BigInteger> MaxDailySponsorPerUserQueryAsync(MaxDailySponsorPerUserFunction maxDailySponsorPerUserFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxDailySponsorPerUserFunction, BigInteger>(maxDailySponsorPerUserFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> MaxDailySponsorPerUserQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxDailySponsorPerUserFunction, BigInteger>(null, blockParameter);
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

        public virtual Task<string> SetMaxDailySponsorPerUserRequestAsync(SetMaxDailySponsorPerUserFunction setMaxDailySponsorPerUserFunction)
        {
             return ContractHandler.SendRequestAsync(setMaxDailySponsorPerUserFunction);
        }

        public virtual Task<TransactionReceipt> SetMaxDailySponsorPerUserRequestAndWaitForReceiptAsync(SetMaxDailySponsorPerUserFunction setMaxDailySponsorPerUserFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setMaxDailySponsorPerUserFunction, cancellationToken);
        }

        public virtual Task<string> SetMaxDailySponsorPerUserRequestAsync(BigInteger amount)
        {
            var setMaxDailySponsorPerUserFunction = new SetMaxDailySponsorPerUserFunction();
                setMaxDailySponsorPerUserFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(setMaxDailySponsorPerUserFunction);
        }

        public virtual Task<TransactionReceipt> SetMaxDailySponsorPerUserRequestAndWaitForReceiptAsync(BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var setMaxDailySponsorPerUserFunction = new SetMaxDailySponsorPerUserFunction();
                setMaxDailySponsorPerUserFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setMaxDailySponsorPerUserFunction, cancellationToken);
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
                typeof(DailySponsoredFunction),
                typeof(MaxDailySponsorPerUserFunction),
                typeof(PostOpFunction),
                typeof(SetMaxDailySponsorPerUserFunction),
                typeof(ValidatePaymasterUserOpFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(SponsorLimitSetEventDTO)
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
