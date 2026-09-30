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
using Nethereum.AccountAbstraction.Contracts.Rules.RuleRegistry.ContractDefinition;

namespace Nethereum.AccountAbstraction.Contracts.Rules.RuleRegistry
{
    public partial class RuleRegistryService: RuleRegistryServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, RuleRegistryDeployment ruleRegistryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<RuleRegistryDeployment>().SendRequestAndWaitForReceiptAsync(ruleRegistryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, RuleRegistryDeployment ruleRegistryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<RuleRegistryDeployment>().SendRequestAsync(ruleRegistryDeployment);
        }

        public static async Task<RuleRegistryService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, RuleRegistryDeployment ruleRegistryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, ruleRegistryDeployment, cancellationTokenSource);
            return new RuleRegistryService(web3, receipt.ContractAddress);
        }

        public RuleRegistryService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class RuleRegistryServiceBase: ContractWeb3ServiceBase
    {

        public RuleRegistryServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> CloseRegistrationRequestAsync(CloseRegistrationFunction closeRegistrationFunction)
        {
             return ContractHandler.SendRequestAsync(closeRegistrationFunction);
        }

        public virtual Task<string> CloseRegistrationRequestAsync()
        {
             return ContractHandler.SendRequestAsync<CloseRegistrationFunction>();
        }

        public virtual Task<TransactionReceipt> CloseRegistrationRequestAndWaitForReceiptAsync(CloseRegistrationFunction closeRegistrationFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(closeRegistrationFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> CloseRegistrationRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<CloseRegistrationFunction>(null, cancellationToken);
        }

        public Task<byte[]> ExecuteQueryAsync(ExecuteFunction executeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ExecuteFunction, byte[]>(executeFunction, blockParameter);
        }

        
        public virtual Task<byte[]> ExecuteQueryAsync(byte[] ruleId, byte[] input, BlockParameter blockParameter = null)
        {
            var executeFunction = new ExecuteFunction();
                executeFunction.RuleId = ruleId;
                executeFunction.Input = input;
            
            return ContractHandler.QueryAsync<ExecuteFunction, byte[]>(executeFunction, blockParameter);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public virtual Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public virtual Task<string> RegisterRuleRequestAsync(RegisterRuleFunction registerRuleFunction)
        {
             return ContractHandler.SendRequestAsync(registerRuleFunction);
        }

        public virtual Task<TransactionReceipt> RegisterRuleRequestAndWaitForReceiptAsync(RegisterRuleFunction registerRuleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerRuleFunction, cancellationToken);
        }

        public virtual Task<string> RegisterRuleRequestAsync(byte[] ruleId, string rule)
        {
            var registerRuleFunction = new RegisterRuleFunction();
                registerRuleFunction.RuleId = ruleId;
                registerRuleFunction.Rule = rule;
            
             return ContractHandler.SendRequestAsync(registerRuleFunction);
        }

        public virtual Task<TransactionReceipt> RegisterRuleRequestAndWaitForReceiptAsync(byte[] ruleId, string rule, CancellationTokenSource cancellationToken = null)
        {
            var registerRuleFunction = new RegisterRuleFunction();
                registerRuleFunction.RuleId = ruleId;
                registerRuleFunction.Rule = rule;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerRuleFunction, cancellationToken);
        }

        public Task<bool> RegistrationClosedQueryAsync(RegistrationClosedFunction registrationClosedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistrationClosedFunction, bool>(registrationClosedFunction, blockParameter);
        }

        
        public virtual Task<bool> RegistrationClosedQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistrationClosedFunction, bool>(null, blockParameter);
        }

        public Task<string> RuleOfQueryAsync(RuleOfFunction ruleOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RuleOfFunction, string>(ruleOfFunction, blockParameter);
        }

        
        public virtual Task<string> RuleOfQueryAsync(byte[] returnValue1, BlockParameter blockParameter = null)
        {
            var ruleOfFunction = new RuleOfFunction();
                ruleOfFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<RuleOfFunction, string>(ruleOfFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(CloseRegistrationFunction),
                typeof(ExecuteFunction),
                typeof(OwnerFunction),
                typeof(RegisterRuleFunction),
                typeof(RegistrationClosedFunction),
                typeof(RuleOfFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(RegistrationClosedEventDTO),
                typeof(RuleRegisteredEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(NotOwnerError),
                typeof(RegistrationIsClosedError),
                typeof(RuleAlreadySetError),
                typeof(UnknownRuleError),
                typeof(ZeroRuleError)
            };
        }
    }
}
