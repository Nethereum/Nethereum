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
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.ERC20SpendingLimitPolicy.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.ERC20SpendingLimitPolicy
{
    public partial class ERC20SpendingLimitPolicyService: ERC20SpendingLimitPolicyServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, ERC20SpendingLimitPolicyDeployment eRC20SpendingLimitPolicyDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ERC20SpendingLimitPolicyDeployment>().SendRequestAndWaitForReceiptAsync(eRC20SpendingLimitPolicyDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, ERC20SpendingLimitPolicyDeployment eRC20SpendingLimitPolicyDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ERC20SpendingLimitPolicyDeployment>().SendRequestAsync(eRC20SpendingLimitPolicyDeployment);
        }

        public static async Task<ERC20SpendingLimitPolicyService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, ERC20SpendingLimitPolicyDeployment eRC20SpendingLimitPolicyDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, eRC20SpendingLimitPolicyDeployment, cancellationTokenSource);
            return new ERC20SpendingLimitPolicyService(web3, receipt.ContractAddress);
        }

        public ERC20SpendingLimitPolicyService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class ERC20SpendingLimitPolicyServiceBase: ContractWeb3ServiceBase
    {

        public ERC20SpendingLimitPolicyServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<string> CheckActionRequestAsync(CheckActionFunction checkActionFunction)
        {
             return ContractHandler.SendRequestAsync(checkActionFunction);
        }

        public virtual Task<TransactionReceipt> CheckActionRequestAndWaitForReceiptAsync(CheckActionFunction checkActionFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(checkActionFunction, cancellationToken);
        }

        public virtual Task<string> CheckActionRequestAsync(byte[] id, string account, string target, BigInteger value, byte[] callData)
        {
            var checkActionFunction = new CheckActionFunction();
                checkActionFunction.Id = id;
                checkActionFunction.Account = account;
                checkActionFunction.Target = target;
                checkActionFunction.Value = value;
                checkActionFunction.CallData = callData;
            
             return ContractHandler.SendRequestAsync(checkActionFunction);
        }

        public virtual Task<TransactionReceipt> CheckActionRequestAndWaitForReceiptAsync(byte[] id, string account, string target, BigInteger value, byte[] callData, CancellationTokenSource cancellationToken = null)
        {
            var checkActionFunction = new CheckActionFunction();
                checkActionFunction.Id = id;
                checkActionFunction.Account = account;
                checkActionFunction.Target = target;
                checkActionFunction.Value = value;
                checkActionFunction.CallData = callData;
            
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
                typeof(CheckActionFunction),
                typeof(InitializeWithMultiplexerFunction),
                typeof(SupportsInterfaceFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(PolicySetEventDTO),
                typeof(TokenSpentEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(InvalidLimitError),
                typeof(InvalidTokenAddressError)
            };
        }
    }
}
