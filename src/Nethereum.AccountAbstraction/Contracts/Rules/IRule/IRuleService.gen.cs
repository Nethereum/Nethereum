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
using Nethereum.AccountAbstraction.Contracts.Rules.IRule.ContractDefinition;

namespace Nethereum.AccountAbstraction.Contracts.Rules.IRule
{
    public partial class IRuleService: IRuleServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, IRuleDeployment iRuleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<IRuleDeployment>().SendRequestAndWaitForReceiptAsync(iRuleDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, IRuleDeployment iRuleDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<IRuleDeployment>().SendRequestAsync(iRuleDeployment);
        }

        public static async Task<IRuleService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, IRuleDeployment iRuleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iRuleDeployment, cancellationTokenSource);
            return new IRuleService(web3, receipt.ContractAddress);
        }

        public IRuleService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class IRuleServiceBase: ContractWeb3ServiceBase
    {

        public IRuleServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<byte[]> EvaluateQueryAsync(EvaluateFunction evaluateFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EvaluateFunction, byte[]>(evaluateFunction, blockParameter);
        }

        
        public virtual Task<byte[]> EvaluateQueryAsync(byte[] input, BlockParameter blockParameter = null)
        {
            var evaluateFunction = new EvaluateFunction();
                evaluateFunction.Input = input;
            
            return ContractHandler.QueryAsync<EvaluateFunction, byte[]>(evaluateFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(EvaluateFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {

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
