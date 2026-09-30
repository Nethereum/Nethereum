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
using Nethereum.AccountAbstraction.Contracts.Rules.BadOutputRule.ContractDefinition;

namespace Nethereum.AccountAbstraction.Contracts.Rules.BadOutputRule
{
    public partial class BadOutputRuleService: BadOutputRuleServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, BadOutputRuleDeployment badOutputRuleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<BadOutputRuleDeployment>().SendRequestAndWaitForReceiptAsync(badOutputRuleDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, BadOutputRuleDeployment badOutputRuleDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<BadOutputRuleDeployment>().SendRequestAsync(badOutputRuleDeployment);
        }

        public static async Task<BadOutputRuleService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, BadOutputRuleDeployment badOutputRuleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, badOutputRuleDeployment, cancellationTokenSource);
            return new BadOutputRuleService(web3, receipt.ContractAddress);
        }

        public BadOutputRuleService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class BadOutputRuleServiceBase: ContractWeb3ServiceBase
    {

        public BadOutputRuleServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<byte[]> EvaluateQueryAsync(EvaluateFunction evaluateFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EvaluateFunction, byte[]>(evaluateFunction, blockParameter);
        }

        
        public virtual Task<byte[]> EvaluateQueryAsync(byte[] returnValue1, BlockParameter blockParameter = null)
        {
            var evaluateFunction = new EvaluateFunction();
                evaluateFunction.ReturnValue1 = returnValue1;
            
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
