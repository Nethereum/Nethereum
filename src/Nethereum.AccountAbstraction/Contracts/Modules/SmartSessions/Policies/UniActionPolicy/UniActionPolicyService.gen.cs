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
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.UniActionPolicy.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.UniActionPolicy
{
    public partial class UniActionPolicyService: UniActionPolicyServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, UniActionPolicyDeployment uniActionPolicyDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<UniActionPolicyDeployment>().SendRequestAndWaitForReceiptAsync(uniActionPolicyDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, UniActionPolicyDeployment uniActionPolicyDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<UniActionPolicyDeployment>().SendRequestAsync(uniActionPolicyDeployment);
        }

        public static async Task<UniActionPolicyService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, UniActionPolicyDeployment uniActionPolicyDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, uniActionPolicyDeployment, cancellationTokenSource);
            return new UniActionPolicyService(web3, receipt.ContractAddress);
        }

        public UniActionPolicyService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class UniActionPolicyServiceBase: ContractWeb3ServiceBase
    {

        public UniActionPolicyServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<ActionConfigsOutputDTO> ActionConfigsQueryAsync(ActionConfigsFunction actionConfigsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<ActionConfigsFunction, ActionConfigsOutputDTO>(actionConfigsFunction, blockParameter);
        }

        public virtual Task<ActionConfigsOutputDTO> ActionConfigsQueryAsync(byte[] id, string msgSender, string userOpSender, BlockParameter blockParameter = null)
        {
            var actionConfigsFunction = new ActionConfigsFunction();
                actionConfigsFunction.Id = id;
                actionConfigsFunction.MsgSender = msgSender;
                actionConfigsFunction.UserOpSender = userOpSender;
            
            return ContractHandler.QueryDeserializingToObjectAsync<ActionConfigsFunction, ActionConfigsOutputDTO>(actionConfigsFunction, blockParameter);
        }

        public virtual Task<string> CheckActionRequestAsync(CheckActionFunction checkActionFunction)
        {
             return ContractHandler.SendRequestAsync(checkActionFunction);
        }

        public virtual Task<TransactionReceipt> CheckActionRequestAndWaitForReceiptAsync(CheckActionFunction checkActionFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(checkActionFunction, cancellationToken);
        }

        public virtual Task<string> CheckActionRequestAsync(byte[] id, string account, string returnValue3, BigInteger value, byte[] data)
        {
            var checkActionFunction = new CheckActionFunction();
                checkActionFunction.Id = id;
                checkActionFunction.Account = account;
                checkActionFunction.ReturnValue3 = returnValue3;
                checkActionFunction.Value = value;
                checkActionFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(checkActionFunction);
        }

        public virtual Task<TransactionReceipt> CheckActionRequestAndWaitForReceiptAsync(byte[] id, string account, string returnValue3, BigInteger value, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var checkActionFunction = new CheckActionFunction();
                checkActionFunction.Id = id;
                checkActionFunction.Account = account;
                checkActionFunction.ReturnValue3 = returnValue3;
                checkActionFunction.Value = value;
                checkActionFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(checkActionFunction, cancellationToken);
        }

        public virtual Task<string> InitializeWithMultiplexerRequestAsync(InitializeWithMultiplexerFunction initializeWithMultiplexerFunction)
        {
             return ContractHandler.SendRequestAsync(initializeWithMultiplexerFunction);
        }

        public virtual Task<TransactionReceipt> InitializeWithMultiplexerRequestAndWaitForReceiptAsync(InitializeWithMultiplexerFunction initializeWithMultiplexerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeWithMultiplexerFunction, cancellationToken);
        }

        public virtual Task<string> InitializeWithMultiplexerRequestAsync(string account, byte[] configId, byte[] initData)
        {
            var initializeWithMultiplexerFunction = new InitializeWithMultiplexerFunction();
                initializeWithMultiplexerFunction.Account = account;
                initializeWithMultiplexerFunction.ConfigId = configId;
                initializeWithMultiplexerFunction.InitData = initData;
            
             return ContractHandler.SendRequestAsync(initializeWithMultiplexerFunction);
        }

        public virtual Task<TransactionReceipt> InitializeWithMultiplexerRequestAndWaitForReceiptAsync(string account, byte[] configId, byte[] initData, CancellationTokenSource cancellationToken = null)
        {
            var initializeWithMultiplexerFunction = new InitializeWithMultiplexerFunction();
                initializeWithMultiplexerFunction.Account = account;
                initializeWithMultiplexerFunction.ConfigId = configId;
                initializeWithMultiplexerFunction.InitData = initData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeWithMultiplexerFunction, cancellationToken);
        }

        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        
        public virtual Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceID, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
                supportsInterfaceFunction.InterfaceID = interfaceID;
            
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(ActionConfigsFunction),
                typeof(CheckActionFunction),
                typeof(InitializeWithMultiplexerFunction),
                typeof(SupportsInterfaceFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(PolicySetEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(PolicyNotInitializedError),
                typeof(ValueLimitExceededError)
            };
        }
    }
}
