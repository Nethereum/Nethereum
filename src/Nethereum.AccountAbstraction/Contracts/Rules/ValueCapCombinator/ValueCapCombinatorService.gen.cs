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
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapCombinator.ContractDefinition;

namespace Nethereum.AccountAbstraction.Contracts.Rules.ValueCapCombinator
{
    public partial class ValueCapCombinatorService: ValueCapCombinatorServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, ValueCapCombinatorDeployment valueCapCombinatorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ValueCapCombinatorDeployment>().SendRequestAndWaitForReceiptAsync(valueCapCombinatorDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, ValueCapCombinatorDeployment valueCapCombinatorDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ValueCapCombinatorDeployment>().SendRequestAsync(valueCapCombinatorDeployment);
        }

        public static async Task<ValueCapCombinatorService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, ValueCapCombinatorDeployment valueCapCombinatorDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, valueCapCombinatorDeployment, cancellationTokenSource);
            return new ValueCapCombinatorService(web3, receipt.ContractAddress);
        }

        public ValueCapCombinatorService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class ValueCapCombinatorServiceBase: ContractWeb3ServiceBase
    {

        public ValueCapCombinatorServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<BigInteger> CheckActionQueryAsync(CheckActionFunction checkActionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CheckActionFunction, BigInteger>(checkActionFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> CheckActionQueryAsync(byte[] id, string account, string returnValue3, BigInteger value, byte[] returnValue5, BlockParameter blockParameter = null)
        {
            var checkActionFunction = new CheckActionFunction();
                checkActionFunction.Id = id;
                checkActionFunction.Account = account;
                checkActionFunction.ReturnValue3 = returnValue3;
                checkActionFunction.Value = value;
                checkActionFunction.ReturnValue5 = returnValue5;
            
            return ContractHandler.QueryAsync<CheckActionFunction, BigInteger>(checkActionFunction, blockParameter);
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

        public Task<bool> IsInitializedQueryAsync(IsInitializedFunction isInitializedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsInitializedFunction, bool>(isInitializedFunction, blockParameter);
        }

        
        public virtual Task<bool> IsInitializedQueryAsync(byte[] id, string account, BlockParameter blockParameter = null)
        {
            var isInitializedFunction = new IsInitializedFunction();
                isInitializedFunction.Id = id;
                isInitializedFunction.Account = account;
            
            return ContractHandler.QueryAsync<IsInitializedFunction, bool>(isInitializedFunction, blockParameter);
        }

        public Task<string> RegistryQueryAsync(RegistryFunction registryFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistryFunction, string>(registryFunction, blockParameter);
        }

        
        public virtual Task<string> RegistryQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RegistryFunction, string>(null, blockParameter);
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
                typeof(IsInitializedFunction),
                typeof(RegistryFunction),
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
                typeof(InvalidRuleOutputError),
                typeof(NotInitializedError),
                typeof(UnknownRuleError)
            };
        }
    }
}
